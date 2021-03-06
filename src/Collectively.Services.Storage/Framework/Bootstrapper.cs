﻿using System.Collections.Generic;
using System.Globalization;
using Autofac;
using Collectively.Common.Exceptionless;
using Collectively.Common.Mongo;
using Collectively.Common.NancyFx;
using Collectively.Common.Security;
using Collectively.Services.Storage.Framework.IoC;
using Collectively.Services.Storage.Providers;
using Collectively.Services.Storage.Repositories;
using Collectively.Services.Storage.Settings;
using Collectively.Services.Storage.Services;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Bootstrappers.Autofac;
using Nancy.Configuration;
using Newtonsoft.Json;
using Serilog;
using RawRabbit.Configuration;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;
using Collectively.Common.Extensions;
using Collectively.Common.Services;
using Collectively.Common.RabbitMq;
using Collectively.Common.Caching;
using Collectively.Common.ServiceClients;
using Microsoft.Extensions.DependencyInjection;
using Autofac.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using Collectively.Services.Storage.Models.Remarks;

namespace Collectively.Services.Storage.Framework
{
    public class Bootstrapper : AutofacNancyBootstrapper
    {
        private static readonly ILogger Logger = Log.Logger;
        private static IExceptionHandler _exceptionHandler;
        private static readonly string DecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        private static readonly string InvalidDecimalSeparator = DecimalSeparator == "." ? "," : ".";
        private readonly IConfiguration _configuration;
        private IServiceCollection _services;

        public static ILifetimeScope LifeTimeScope { get; private set; }

        public Bootstrapper(IConfiguration configuration, IServiceCollection services)
        {
            _configuration = configuration;
            _services = services;
        }

#if DEBUG
        public override void Configure(INancyEnvironment environment)
        {
            base.Configure(environment);
            environment.Tracing(enabled: false, displayErrorTraces: true);
        }
#endif

        protected override void ConfigureApplicationContainer(ILifetimeScope container)
        {
            Logger.Information("Collectively.Services.Storage Configuring application container");
            base.ConfigureApplicationContainer(container);

            container.Update(builder =>
            {
                builder.Populate(_services);
                builder.RegisterType<CustomJsonSerializer>().As<JsonSerializer>().SingleInstance();
                builder.RegisterInstance(_configuration.GetSettings<GeneralSettings>()).SingleInstance();
                builder.RegisterInstance(_configuration.GetSettings<MongoDbSettings>()).SingleInstance();
                builder.RegisterInstance(_configuration.GetSettings<RedisSettings>()).SingleInstance();
                builder.RegisterModule<MongoDbModule>();
                builder.RegisterType<MongoDbInitializer>().As<IDatabaseInitializer>();
                builder.RegisterType<DatabaseSeeder>().As<IDatabaseSeeder>().InstancePerLifetimeScope();
                builder.RegisterType<OperationRepository>().As<IOperationRepository>().InstancePerLifetimeScope();
                builder.RegisterType<RemarkRepository>().As<IRemarkRepository>().InstancePerLifetimeScope();
                builder.RegisterType<RemarkCategoryRepository>().As<IRemarkCategoryRepository>().InstancePerLifetimeScope();
                builder.RegisterType<ReportRepository>().As<IReportRepository>().InstancePerLifetimeScope();
                builder.RegisterType<TagRepository>().As<ITagRepository>().InstancePerLifetimeScope();
                builder.RegisterType<UserRepository>().As<IUserRepository>().InstancePerLifetimeScope();
                builder.RegisterType<UserSessionRepository>().As<IUserSessionRepository>().InstancePerLifetimeScope();
                builder.RegisterType<GroupRepository>().As<IGroupRepository>().InstancePerLifetimeScope();
                builder.RegisterType<UserNotificationSettingsRepository>().As<IUserNotificationSettingsRepository>().InstancePerLifetimeScope();
                builder.RegisterType<OrganizationRepository>().As<IOrganizationRepository>().InstancePerLifetimeScope();
                builder.RegisterType<ProviderClient>().As<IProviderClient>().InstancePerLifetimeScope();
                builder.RegisterType<OperationProvider>().As<IOperationProvider>().InstancePerLifetimeScope();
                builder.RegisterType<RemarkProvider>().As<IRemarkProvider>().InstancePerLifetimeScope();
                builder.RegisterType<StatisticsProvider>().As<IStatisticsProvider>().InstancePerLifetimeScope();
                builder.RegisterType<UserProvider>().As<IUserProvider>().InstancePerLifetimeScope();
                builder.RegisterType<NotificationProvider>().As<INotificationProvider>().InstancePerLifetimeScope();
                builder.RegisterType<GroupProvider>().As<IGroupProvider>().InstancePerLifetimeScope();
                builder.RegisterType<OrganizationProvider>().As<IOrganizationProvider>().InstancePerLifetimeScope();
                builder.RegisterType<Handler>().As<IHandler>().InstancePerLifetimeScope();
                builder.RegisterInstance(_configuration.GetSettings<ExceptionlessSettings>()).SingleInstance();
                builder.RegisterType<ExceptionlessExceptionHandler>().As<IExceptionHandler>().SingleInstance();
                builder.RegisterModule<CommandHandlersModule>();
                builder.RegisterModule<EventHandlersModule>();
                builder.RegisterModule<ServiceClientModule>();
                builder.RegisterModule<ServiceClientsModule>();
                builder.RegisterModule<RedisModule>();
                builder.RegisterType<AccountStateService>().As<IAccountStateService>().InstancePerLifetimeScope();
                builder.RegisterType<OperationCache>().As<IOperationCache>().InstancePerLifetimeScope();
                builder.RegisterType<RemarkCache>().As<IRemarkCache>().InstancePerLifetimeScope();
                builder.RegisterType<GroupCache>().As<IGroupCache>().InstancePerLifetimeScope();
                builder.RegisterType<OrganizationCache>().As<IOrganizationCache>().InstancePerLifetimeScope();
                builder.RegisterType<UserCache>().As<IUserCache>().InstancePerLifetimeScope();
                builder.RegisterType<UserNotificationSettingsCache>().As<IUserNotificationSettingsCache>().InstancePerLifetimeScope();
                SecurityContainer.Register(builder, _configuration);
                RabbitMqContainer.Register(builder, _configuration.GetSettings<RawRabbitConfiguration>());
            });
            LifeTimeScope = container;
        }

        protected override void RequestStartup(ILifetimeScope container, IPipelines pipelines, NancyContext context)
        {
            pipelines.SetupTokenAuthentication(container.Resolve<IJwtTokenHandler>());
            pipelines.OnError.AddItemToEndOfPipeline((ctx, ex) =>
            {
                _exceptionHandler.Handle(ex, ctx.ToExceptionData(),
                    "Request details", "Collectively", "Service", "Storage");

                return ctx.Response;
            });
        }

        protected override void ApplicationStartup(ILifetimeScope container, IPipelines pipelines)
        {
            var databaseSettings = container.Resolve<MongoDbSettings>();
            var databaseInitializer = container.Resolve<IDatabaseInitializer>();
            databaseInitializer.InitializeAsync();
            BsonClassMap.RegisterClassMap<RemarkGroup>(x => 
            {
                x.AutoMap();
                x.UnmapMember(m => m.Criteria);
                x.UnmapMember(m => m.Members);
                x.UnmapMember(m => m.MemberCriteria);
                x.UnmapMember(m => m.MemberRole);
            });
            BsonClassMap.RegisterClassMap<BasicRemark>(x => 
            {
                x.AutoMap();
                x.UnmapMember(m => m.Distance);
                x.UnmapMember(m => m.SmallPhotoUrl);
                x.UnmapMember(m => m.Photo);
            });
            var databaseSeeder = container.Resolve<IDatabaseSeeder>();
            databaseSeeder.SeedAsync();

            pipelines.BeforeRequest += (ctx) =>
            {
                FixNumberFormat(ctx);

                return null;
            };
            pipelines.AfterRequest += (ctx) =>
            {
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                ctx.Response.Headers.Add("Access-Control-Allow-Methods", "POST,PUT,GET,OPTIONS,DELETE");
                ctx.Response.Headers.Add("Access-Control-Allow-Headers",
                    "Authorization, Origin, X-Requested-With, Content-Type, Accept, X-Total-Count");
            };
            _exceptionHandler = container.Resolve<IExceptionHandler>();
            Logger.Information("Collectively.Services.Storage API has started.");
        }

        private void FixNumberFormat(NancyContext ctx)
        {
            if (ctx.Request.Query == null)
                return;

            var fixedNumbers = new Dictionary<string, double>();
            foreach (var key in ctx.Request.Query)
            {
                var value = ctx.Request.Query[key].ToString();
                if (!value.Contains(InvalidDecimalSeparator))
                    continue;

                var number = 0;
                if (int.TryParse(value.Split(InvalidDecimalSeparator[0])[0], out number))
                    fixedNumbers[key] = double.Parse(value.Replace(InvalidDecimalSeparator, DecimalSeparator));
            }
            foreach (var fixedNumber in fixedNumbers)
            {
                ctx.Request.Query[fixedNumber.Key] = fixedNumber.Value;
            }
        }
    }
}