﻿using AutoMapper;
using Repositories.Entities;
using Repositories.Interfaces;
using Repositories.Models.RatingModels;
using Services.Common;
using Services.Interfaces;
using Services.Models.RatingModels;
using Services.Models.ResponseModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Services
{
    public class RatingService : IRatingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public RatingService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }
        public async Task<ResponseModel> AddComment(RatingCommentCreateModel ratingCommentCreateModel)
        {
            var rating = await _unitOfWork.RatingRepository.GetAsync(ratingCommentCreateModel.RatingId);
            if (rating == null)
            {
                return new ResponseModel { Status = false, Message = "Rating not found." };
            }

            var comment = new RatingComment
            {
                RatingId = ratingCommentCreateModel.RatingId,
                CommentText = ratingCommentCreateModel.CommentText,
                AccountId = ratingCommentCreateModel.AccountId, 
                ParentCommentId = ratingCommentCreateModel.ParentCommentId
            };

            await _unitOfWork.CommentRepository.AddAsync(comment);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseModel { Status = true, Message = "Comment added successfully." };
        }

        public async Task<ResponseModel> CreateRating(RatingCreateModel ratingCreateModel)
        {
            var rating = new Rating
            {
                PodId = ratingCreateModel.PodId,
                RatingValue = ratingCreateModel.RatingValue,
                Comments = ratingCreateModel.Comments,
                CustomerId = ratingCreateModel.CustomerId,
            };

            await _unitOfWork.RatingRepository.AddAsync(rating);
            await _unitOfWork.SaveChangeAsync();

            return new ResponseModel { Status = true, Message = "Rating created successfully." };
        }

        public async Task<ResponseDataModel<RatingModel>> GetRatingById(Guid ratingId)
        {
            var rating = await _unitOfWork.RatingRepository.GetAsync(ratingId);
            var ratingModel = _mapper.Map<RatingModel>(rating);
            return new ResponseDataModel<RatingModel> { Data = ratingModel, Status = true };
        }

        public async Task<Pagination<RatingModel>> GetRatingsByPodAsync(RatingFilterModel model)
        {
            var queryResult = await _unitOfWork.RatingRepository.GetAllAsync(
                filter: r => (model.PodId == null || r.PodId == model.PodId) &&
                             (model.AccountId == null || r.CustomerId == model.AccountId) &&
                             (model.RatingValue == null || r.RatingValue == model.RatingValue),
                include: "Customer,Pod,CommentsList",
                pageIndex: model.PageIndex,
                pageSize: model.PageSize
            );

            var ratings = _mapper.Map<List<RatingModel>>(queryResult.Data);
            return new Pagination<RatingModel>(ratings, model.PageIndex, model.PageSize, queryResult.TotalCount);
        }

    }
}
