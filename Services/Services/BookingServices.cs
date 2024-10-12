﻿using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Repositories.Entities;
using Repositories.Enums;
using Repositories.Interfaces;
using Repositories.Models.BookingModels;
using Services.Common;
using Services.Interfaces;
using Services.Models.BookingModels;
using Services.Models.ResponseModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Services
{
    public class BookingServices : IBookingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly UserManager<Account> _userManager;


        public BookingServices(IUnitOfWork unitOfWork, IMapper mapper, UserManager<Account> userManager)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _userManager = userManager;
        }

        private async Task<ResponseModel> CheckRoomAvailabilityAsync(Guid podId, DateTime startTime, DateTime endTime)
        {
            var overlappingBookings = await _unitOfWork.BookingRepository.GetAllAsync(
            filter: b => b.PodId == podId && b.PaymentStatus != PaymentStatus.Canceled &&
                     ((b.StartTime <= startTime && b.EndTime > startTime) ||
                      (b.StartTime < endTime && b.EndTime >= endTime) ||
                      (b.StartTime >= startTime && b.EndTime <= endTime)));
            if (overlappingBookings.Data.Any())
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "Room is already booked in the selected time range.",
                };
            }

            return new ResponseModel { Status = true, Message = "Room is available for booking." };
        }

        public async Task<ResponseModel> CreateBookingAsync(BookingCreateModel model)
        {
            var roomAvailability = await CheckRoomAvailabilityAsync(model.PodId, model.StartTime, model.EndTime);
            if (!roomAvailability.Status)
            {
                return roomAvailability;
            }

            var account = await _userManager.FindByIdAsync(model.AccountId.ToString());
            var pod = await _unitOfWork.PodRepository.GetAsync(model.PodId);
            if (account == null || pod == null)
            {
                return new ResponseModel { Status = false, Message = "Invalid account or pod" };
            }

            var totalHours = (model.EndTime - model.StartTime).TotalHours;
            var totalPrice = (decimal)totalHours * pod.PricePerHour;

            decimal totalServicePrice = 0;
            var bookingServices = new List<BookingService>();
            foreach (var serviceModel in model.BookingServices)
            {
                var service = await _unitOfWork.ServiceRepository.GetAsync(serviceModel.ServiceId);
                if (service != null)
                {
                    var serviceTotalPrice = service.UnitPrice * serviceModel.Quantity;
                    totalServicePrice += serviceTotalPrice;

                    bookingServices.Add(new BookingService
                    {
                        ServiceId = serviceModel.ServiceId,
                        Quantity = serviceModel.Quantity,
                        TotalPrice = serviceTotalPrice,
                        ImageUrl = service.ImageUrl
                    });
                }
            }

            totalPrice += totalServicePrice;

            var booking = new Booking
            {
                Code = Guid.NewGuid(),
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                TotalPrice = totalPrice,
                PaymentStatus = PaymentStatus.Pending,
                PaymentMethod = model.PaymentMethod,
                PodId = pod.Id,
                AccountId = account.Id,
                BookingServices = bookingServices
            };

            await _unitOfWork.BookingRepository.AddAsync(booking);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseModel
            {
                Status = true,
                Message = "Booking created successfully"
            };
        }

        public async Task<Pagination<BookingModel>> GetAllBookingsAsync(BookingFilterModel model)
        {
            var queryResult = await _unitOfWork.BookingRepository.GetAllAsync(
                filter: b => (b.IsDeleted == model.isDelete) &&
                             (model.PodId == null || b.PodId == model.PodId) &&
                             (model.AccountId == null || b.AccountId == model.AccountId) &&
                             (model.StartTime == null || b.StartTime >= model.StartTime) &&
                             (model.EndTime == null || b.EndTime <= model.EndTime) &&
                             (model.PaymentStatus == null || b.PaymentStatus == model.PaymentStatus) &&
                             (model.PaymentMethod == null || b.PaymentMethod == model.PaymentMethod),
                include: "Pod.Location,BookingServices.Service",
                pageIndex: model.PageIndex,
                pageSize: model.PageSize
            );

            var bookings = _mapper.Map<List<BookingModel>>(queryResult.Data);

            return new Pagination<BookingModel>(bookings, model.PageIndex, model.PageSize, queryResult.TotalCount);
        }
        public async Task<ResponseDataModel<BookingModel>> GetBookingByIdAsync(Guid bookingId)
        {
            var bookingEntity = await _unitOfWork.BookingRepository.GetAsync(bookingId,
                include: "Pod.Location,BookingServices.Service"
            );

            if (bookingEntity == null || bookingEntity.IsDeleted == true)
            {
                return new ResponseDataModel<BookingModel>
                {
                    Status = false,
                    Message = "Booking not found",
                    Data = null
                };
            }
            var bookingModel = _mapper.Map<BookingModel>(bookingEntity);
            return new ResponseDataModel<BookingModel>
            {
                Status = true,
                Message = "Booking retrieved successfully",
                Data = bookingModel
            };
        }
        public async Task<ResponseModel> UpdateBookingAsync(Guid bookingId, BookingUpdateModel model)
        {
            var booking = await _unitOfWork.BookingRepository.GetAsync(bookingId,
                include: "BookingServices.Service,Pod"
            );

            if (booking == null)
            {
                return new ResponseModel { Status = false, Message = "Booking not found" };
            }

            booking.StartTime = model.StartTime;
            booking.EndTime = model.EndTime;
            booking.PaymentMethod = model.PaymentMethod;

            var totalHours = (model.EndTime - model.StartTime).TotalHours;
            var totalPrice = (decimal)totalHours * booking.Pod.PricePerHour;

            foreach (var serviceModel in model.BookingServices)
            {
                var existingService = booking.BookingServices
                    .FirstOrDefault(bs => bs.ServiceId == serviceModel.ServiceId);

                if (existingService != null)
                {
                    existingService.Quantity = serviceModel.Quantity;
                    existingService.TotalPrice = serviceModel.Quantity * existingService.Service.UnitPrice;
                }
                else
                {
                    var service = await _unitOfWork.ServiceRepository.GetAsync(serviceModel.ServiceId);
                    if (service == null) continue; 

                    var newBookingService = new BookingService
                    {
                        ServiceId = service.Id,
                        Quantity = serviceModel.Quantity,
                        TotalPrice = serviceModel.Quantity * service.UnitPrice,
                        ImageUrl = service.ImageUrl
                    };

                    booking.BookingServices.Add(newBookingService);

                    totalPrice += newBookingService.TotalPrice;
                }

                if (existingService != null)
                {
                    totalPrice += existingService.TotalPrice;
                }
            }
            if (model.UseRewardPoints)
            {
                var rewardPoints = await _unitOfWork.RewardPointsRepository.GetAllAsync(
                    filter: r => r.AccountId == booking.AccountId
                );
                var totalPoints = rewardPoints.Data.Sum(r => r.Points);

                if (totalPoints >= 400) 
                {
                    int pointsToUse = (totalPoints / 400) * 400;
                    int discountPercentage = (pointsToUse / 400) * 10;
                    decimal discountAmount = (totalPrice * discountPercentage) / 100;
                    totalPrice -= discountAmount;
                    totalPoints -= pointsToUse;

                    foreach (var reward in rewardPoints.Data)
                    {
                        if (reward.Points <= pointsToUse)
                        {
                            pointsToUse -= reward.Points;
                            reward.Points = 0;
                        }
                        else
                        {
                            reward.Points -= pointsToUse;
                            pointsToUse = 0;
                        }

                        if (pointsToUse == 0) break;
                    }
                }
            }

            booking.TotalPrice = totalPrice;

            _unitOfWork.BookingRepository.Update(booking);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseModel { Status = true, Message = "Booking updated successfully" };
        }

        public async Task<ResponseModel> DeleteBookingAsync(Guid bookingId)
        {
            var booking = await _unitOfWork.BookingRepository.GetAsync(bookingId);
            if (booking == null)
            {
                return new ResponseModel
                {
                    Status = false,
                    Message = "Can't find Booking in database"
                };

            }
            _unitOfWork.BookingRepository.SoftDelete(booking);
            await _unitOfWork.SaveChangeAsync();
            return new ResponseModel
            {
                Status = true,
                Message = "Delete successful"
            };
        }
    }
}