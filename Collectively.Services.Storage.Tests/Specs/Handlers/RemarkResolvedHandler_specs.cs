﻿using Collectively.Services.Storage.Handlers;
using Collectively.Services.Storage.Repositories;
using Machine.Specifications;
using Moq;
using System;
using System.Collections.Generic;
using Collectively.Common.Services;
using Collectively.Services.Storage.Models.Remarks;
using Collectively.Messages.Events.Remarks;
using Collectively.Messages.Events.Remarks.Models;
using Collectively.Services.Storage.Models.Users;
using It = Machine.Specifications.It;
using RemarkCategory = Collectively.Services.Storage.Models.Remarks.RemarkCategory;

namespace Collectively.Services.Storage.Tests.Specs.Handlers
{
    public class RemarkResolvedHandler_specs
    {
        protected static IHandler Handler;
        protected static RemarkResolvedHandler RemarkResolvedHandler;
        protected static Mock<IRemarkRepository> RemarkRepositoryMock;
        protected static Mock<IUserRepository> UserRepositoryMock;
        protected static Mock<IExceptionHandler> ExceptionHandlerMock;
        protected static RemarkResolved Event;
        protected static Guid RemarkId = Guid.NewGuid();
        protected static string UserId = "UserId";
        protected static RemarkFile Photo;
        protected static DateTime CreatedAt = DateTime.Now - TimeSpan.FromMinutes(5.0);
        protected static DateTime ResolvedAt = DateTime.Now;
        protected static Remark Remark;
        protected static RemarkUser Author;
        protected static RemarkCategory Category;
        protected static Location Location;
        protected static File File;
        protected static User User;

        protected static void Initialize()
        {
            ExceptionHandlerMock = new Mock<IExceptionHandler>();
            Handler = new Handler(ExceptionHandlerMock.Object);
            RemarkRepositoryMock = new Mock<IRemarkRepository>();
            UserRepositoryMock = new Mock<IUserRepository>();

            RemarkResolvedHandler = new RemarkResolvedHandler(Handler,
                RemarkRepositoryMock.Object,
                UserRepositoryMock.Object);

            User = new User
            {
                UserId = UserId,
                Name = "TestUser"
            };
            Photo = new RemarkFile(Guid.NewGuid(), "test.jpg", "small", "http://my-test-photo.com", "metadata");
            Event = new RemarkResolved(Guid.NewGuid(), RemarkId, UserId, User.Name, string.Empty, null, ResolvedAt, new List<RemarkFile>{Photo});
            Author = new RemarkUser
            {
                UserId = UserId,
                Name = "TestUser"
            };
            Category = new RemarkCategory
            {
                Id = Guid.NewGuid(),
                Name = "Test"
            };
            Location = new Location
            {
                Address = "address",
                Coordinates = new[] {1.0, 1.0},
                Type = "point"
            };
            Remark = new Remark
            {
                Id = RemarkId,
                Author = Author,
                Category = Category,
                CreatedAt = CreatedAt,
                Description = "test",
                Location = Location,
                Photos = new List<File>()
            };

            RemarkRepositoryMock.Setup(x => x.GetByIdAsync(Moq.It.IsAny<Guid>()))
                .ReturnsAsync(Remark);
            UserRepositoryMock.Setup(x => x.GetByIdAsync(Moq.It.IsAny<string>()))
                .ReturnsAsync(User);
        }
    }

    [Subject("RemarkResolvedHandler HandleAsync")]
    public class when_invoking_handle_async : RemarkResolvedHandler_specs
    {
        Establish context = () => Initialize();

        Because of = () => RemarkResolvedHandler.HandleAsync(Event).Await();

        It should_fetch_remark = () =>
        {
            RemarkRepositoryMock.Verify(x => x.GetByIdAsync(RemarkId), Times.Once);
        };

        It should_fetch_user = () =>
        {
            UserRepositoryMock.Verify(x => x.GetByIdAsync(UserId), Times.Once);
        };

        It should_update_remark = () =>
        {
            RemarkRepositoryMock.Verify(x => x.UpdateAsync(Moq.It.Is<Remark>(r => r.State.State == Event.State.State
                && r.State.CreatedAt == Event.State.CreatedAt
                && r.State.User.UserId == Event.State.UserId)), Times.Once);
        };
    }

    [Subject("RemarkResolvedHandler HandleAsync")]
    public class when_invoking_handle_async_and_remark_does_not_exist : RemarkResolvedHandler_specs
    {
        Establish context = () =>
        {
            Initialize();
            RemarkRepositoryMock.Setup(x => x.GetByIdAsync(Moq.It.IsAny<Guid>()))
                .ReturnsAsync(null);
        };

        Because of = () => RemarkResolvedHandler.HandleAsync(Event).Await();

        It should_fetch_remark = () =>
        {
            RemarkRepositoryMock.Verify(x => x.GetByIdAsync(RemarkId), Times.Once);
        };

        It should_not_fetch_user = () =>
        {
            UserRepositoryMock.Verify(x => x.GetByIdAsync(UserId), Times.Never);
        };

        It should_not_update_remark = () =>
        {
            RemarkRepositoryMock.Verify(x => x.UpdateAsync(Moq.It.Is<Remark>(r => r.State.State == Event.State.State
                && r.State.CreatedAt == Event.State.CreatedAt
                && r.State.User.UserId == Event.UserId)), Times.Never);
        };
    }

    [Subject("RemarkResolvedHandler HandleAsync")]
    public class when_invoking_handle_async_and_user_does_not_exist : RemarkResolvedHandler_specs
    {
        Establish context = () =>
        {
            Initialize();
            UserRepositoryMock.Setup(x => x.GetByIdAsync(Moq.It.IsAny<string>()))
                .ReturnsAsync(null);
        };

        Because of = () => RemarkResolvedHandler.HandleAsync(Event).Await();

        It should_fetch_remark = () =>
        {
            RemarkRepositoryMock.Verify(x => x.GetByIdAsync(RemarkId), Times.Once);
        };

        It should_fetch_user = () =>
        {
            UserRepositoryMock.Verify(x => x.GetByIdAsync(UserId), Times.Once);
        };

        It should_not_update_remark = () =>
        {
            RemarkRepositoryMock.Verify(x => x.UpdateAsync(Moq.It.Is<Remark>(r => r.State.State == Event.State.State
                && r.State.CreatedAt == Event.State.CreatedAt
                && r.State.User.UserId == Event.UserId)), Times.Never);
        };
    }
}