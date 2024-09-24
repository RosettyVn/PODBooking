﻿using Repositories.Interfaces;
using Repositories.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Common
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _dbContext;
        private readonly IAccountRepository _accountRepository;
        private readonly IRatingRepository _ratingRepository;
        private readonly IPodRepository _podRepository;
        private readonly IRatingCommentRepository _ratingCommentRepository;

        public UnitOfWork(AppDbContext dbContext, IAccountRepository accountRepository, IRatingRepository ratingRepository, IPodRepository podRepository, IRatingCommentRepository ratingCommentRepository)
        {
            _dbContext = dbContext;
            _accountRepository = accountRepository;
            _ratingRepository = ratingRepository;
            _podRepository = podRepository;
            _ratingCommentRepository = ratingCommentRepository;
        }

        public AppDbContext DbContext => _dbContext;
        public IAccountRepository AccountRepository => _accountRepository;
        public IRatingRepository RatingRepository => _ratingRepository;
        public IPodRepository PodRepository => _podRepository;
        public IRatingCommentRepository CommentRepository => _ratingCommentRepository;

        public async Task<int> SaveChangeAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}
