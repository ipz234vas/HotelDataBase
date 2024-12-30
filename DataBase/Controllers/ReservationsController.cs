﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DataBase.Models;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;

namespace DataBase.Controllers
{
    [Authorize]
    public class ReservationsController : Controller
    {
        private readonly HotelDataBaseContext _context;

        public ReservationsController(HotelDataBaseContext context)
        {
            _context = context;
        }

        // GET: Reservations
        public async Task<IActionResult> Index(string sortOrder, string searchString)
        {
            ViewData["CheckInDateSortParm"] = String.IsNullOrEmpty(sortOrder) ? "checkin_desc" : "";
            ViewData["TotalPriceSortParm"] = sortOrder == "totalprice" ? "totalprice_desc" : "totalprice";
            ViewData["CurrentFilter"] = searchString;

            var reservations = _context.Reservation
                .Include(r => r.Customer)
                .Include(r => r.Room)
                    .ThenInclude(room => room.RoomType)
                .AsQueryable();

            // Фільтрація за ім'ям та прізвищем клієнта
            if (!string.IsNullOrEmpty(searchString))
            {
                reservations = reservations.Where(r =>
                    r.Customer.FirstName.Contains(searchString) ||
                    r.Customer.LastName.Contains(searchString));
            }

            // Виконуємо запит до бази даних
            var result = await reservations.ToListAsync();


            // Сортування
            switch (sortOrder)
            {
                case "checkin_desc":
                    reservations = reservations.OrderByDescending(r => r.CheckInDate);
                    break;
                case "totalprice":
                    reservations = reservations.OrderBy(r => r.TotalPrice);
                    break;
                case "totalprice_desc":
                    reservations = reservations.OrderByDescending(r => r.TotalPrice);
                    break;
                default:
                    reservations = reservations.OrderBy(r => r.CheckInDate);
                    break;
            }

            return View(await reservations.ToListAsync());
        }






        // GET: Reservations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reservation = await _context.Reservation
                .Include(r => r.Customer)
                .Include(r => r.Room)
                .ThenInclude(r => r.RoomType)
                .FirstOrDefaultAsync(m => m.ReservationId == id);
            if (reservation == null)
            {
                return NotFound();
            }

            return View(reservation);
        }

        // GET: Reservations/Create
        public IActionResult Create()
        {
            ViewData["CustomerId"] = new SelectList(_context.Customers, "CustomerId", "FullName");
            ViewData["RoomId"] = new SelectList(_context.Rooms.Include(room => room.RoomType), "RoomId", "TypeAndNumber");
            return View();
        }

        // POST: Reservations/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ReservationId,CustomerId,RoomId,CheckInDate,CheckOutDate")] Reservation reservation)
        {
            try
            {


                if (reservation.CheckInDate >= reservation.CheckOutDate)
                {
                    ModelState.AddModelError("", "Check-out date must be after check-in date.");
                }

                //// Перевірка доступності кімнати
                //var isRoomAvailable = !_context.Reservation.Any(r =>
                //    r.RoomId == reservation.RoomId &&
                //    r.CheckOutDate > reservation.CheckInDate &&
                //    r.CheckInDate < reservation.CheckOutDate);

                //if (!isRoomAvailable)
                //{
                //    ModelState.AddModelError("", "The selected room is already reserved for the specified dates.");
                //}

                // Отримання даних кімнати для розрахунку ціни
                var room = await _context.Rooms.Include(r => r.RoomType).FirstOrDefaultAsync(r => r.RoomId == reservation.RoomId);

                if (room == null)
                {
                    ModelState.AddModelError("", "Selected room does not exist.");
                }

                if (ModelState.IsValid)
                {

                    int numberOfDays = (reservation.CheckOutDate - reservation.CheckInDate).Days;
                    if (room.RoomType != null)
                        reservation.TotalPrice = numberOfDays * room.RoomType.Price;

                    _context.Add(reservation);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }

             }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.ToString());
            }
            ViewData["CustomerId"] = new SelectList(_context.Customers, "CustomerId", "FullName", reservation.CustomerId);
            ViewData["RoomId"] = new SelectList(_context.Rooms.Include(room => room.RoomType), "RoomId", "TypeAndNumber", reservation.RoomId);

            return View(reservation);
        }





        // GET: Reservations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reservation = await _context.Reservation.FindAsync(id);
            if (reservation == null)
            {
                return NotFound();
            }
            ViewData["CustomerId"] = new SelectList(_context.Customers, "CustomerId", "FullName", reservation.CustomerId);
            ViewData["RoomId"] = new SelectList(_context.Rooms.Include(room => room.RoomType), "RoomId", "TypeAndNumber", reservation.RoomId);
            return View(reservation);
        }

        // POST: Reservations/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ReservationId,CustomerId,RoomId,CheckInDate,CheckOutDate")] Reservation reservation)
        {
            if (id != reservation.ReservationId)
            {
                return NotFound();
            }

            if (reservation.CheckInDate >= reservation.CheckOutDate)
            {
                ModelState.AddModelError("", "Check-out date must be after check-in date.");
            }


            var room = await _context.Rooms.Include(r => r.RoomType).FirstOrDefaultAsync(r => r.RoomId == reservation.RoomId);

            if (room == null)
            {
                ModelState.AddModelError("", "Selected room does not exist.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Обчислюємо кількість днів
                    int numberOfDays = (reservation.CheckOutDate - reservation.CheckInDate).Days;
                    if (room.RoomType != null)
                        // Розрахунок TotalPrice
                        reservation.TotalPrice = numberOfDays * room.RoomType.Price;

                    // Оновлюємо резервацію
                    _context.Update(reservation);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReservationExists(reservation.ReservationId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["CustomerId"] = new SelectList(_context.Customers, "CustomerId", "FullName", reservation.CustomerId);
            ViewData["RoomId"] = new SelectList(_context.Rooms.Include(room => room.RoomType), "RoomId", "TypeAndNumber", reservation.RoomId);
            return View(reservation);
        }



        // GET: Reservations/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var reservation = await _context.Reservation
                .Include(r => r.Room)
                .ThenInclude(r => r.RoomType)
                .Include(r => r.Customer)
                .FirstOrDefaultAsync(m => m.ReservationId == id);

            if (reservation == null)
            {
                return NotFound();
            }

            return View(reservation);
        }

        // POST: Reservations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var sqlQuery = "DELETE FROM Reservation WHERE ReservationID = @id";
            await _context.Database.ExecuteSqlRawAsync(sqlQuery, new SqlParameter("@id", id));
            return RedirectToAction(nameof(Index));
        }


        private bool ReservationExists(int id)
        {
            return _context.Reservation.Any(e => e.ReservationId == id);
        }

    }
}