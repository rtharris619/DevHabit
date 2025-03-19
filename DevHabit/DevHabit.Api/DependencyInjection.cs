using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using DevHabit.Api.Middleware;
using DevHabit.Api.Services.Sorting;
using DevHabit.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json.Serialization;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Asp.Versioning;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Identity;
using DevHabit.Api.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Net.Http.Headers;
using Quartz;
using DevHabit.Api.Jobs;
using DevHabit.Api.DTOs.Entries;
using Refit;
using Polly;
using Microsoft.Extensions.Http.Resilience;
using DevHabit.Api.Extensions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace DevHabit.Api;

public static class DependencyInjection
{
    public static WebApplicationBuilder AddApiServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers(options =>
        {
            options.ReturnHttpNotAcceptable = true;
        })
        .AddNewtonsoftJson(options => options.SerializerSettings.ContractResolver =
            new CamelCasePropertyNamesContractResolver())
        .AddXmlSerializerFormatters();

        builder.Services.Configure<MvcOptions>(options =>
        {
            NewtonsoftJsonOutputFormatter formatter = options.OutputFormatters
                .OfType<NewtonsoftJsonOutputFormatter>()
                .First();
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.JsonV1);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.JsonV2);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJson);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJsonV1);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJsonV2);
        });

        builder.Services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1.0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionSelector = new DefaultApiVersionSelector(options);

                options.ApiVersionReader = ApiVersionReader.Combine(
                    new MediaTypeApiVersionReader(),
                    new MediaTypeApiVersionReaderBuilder()
                        .Template("application/vnd.dev-habit.hateoas.{version}+json")
                        .Build());
            })
            .AddMvc();

        builder.Services.AddOpenApi();

        builder.Services.AddResponseCaching();

        return builder;
    }

    public static WebApplicationBuilder AddErrorHandling(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
            };
        });

        builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        return builder;
    }

    public static WebApplicationBuilder AddDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options
            .UseNpgsql(
                builder.Configuration.GetConnectionString("Database"),
                npgsqlOptions => npgsqlOptions
                    .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
            .UseSnakeCaseNamingConvention());

        builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
            options
            .UseNpgsql(
                builder.Configuration.GetConnectionString("Database"),
                npgsqlOptions => npgsqlOptions
                    .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Identity))
            .UseSnakeCaseNamingConvention());

        return builder;
    }

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
            .WithTracing(tracing => tracing
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation())
            .UseOtlpExporter();

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
        });

        return builder;
    }

    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddValidatorsFromAssemblyContaining<Program>();

        builder.Services.AddTransient<SortMappingProvider>();

        builder.Services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<HabitDto, Habit>>(_ =>
            HabitMappings.SortMapping);

        builder.Services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<EntryDto, Entry>>(_ =>
            EntryMappings.SortMapping);

        builder.Services.AddTransient<DataShapingService>();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<LinkService>();

        builder.Services.AddTransient<TokenProvider>();

        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<UserContext>();

        // GitHub

        builder.Services.AddScoped<GitHubAccessTokenService>();
        builder.Services.AddTransient<GitHubService>();

        // Global Resilience Handler
        builder.Services.AddHttpClient().ConfigureHttpClientDefaults(b => b.AddStandardResilienceHandler());

        //builder.Services.AddTransient<RefitGitHubService>();

        builder.Services
            .AddHttpClient("github")
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.github.com");

                client.DefaultRequestHeaders
                    .UserAgent.Add(new ProductInfoHeaderValue("DevHabit", "1.0"));

                client.DefaultRequestHeaders
                    .Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            });

        builder.Services.AddTransient<DelayHandler>();

        builder.Services
            .AddRefitClient<IGitHubApi>(new RefitSettings
            {
                ContentSerializer = new NewtonsoftJsonContentSerializer()
            })
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://api.github.com"));
        //.AddHttpMessageHandler<DelayHandler>();
        // Override the default global resilience handler
        //.InternalRemoveAllResilienceHandlers()
        //.AddResilienceHandler("custom", pipeline =>
        //{
        //    pipeline.AddTimeout(TimeSpan.FromSeconds(5));

        //    pipeline.AddRetry(new HttpRetryStrategyOptions
        //    {
        //        MaxRetryAttempts = 3,
        //        BackoffType = DelayBackoffType.Exponential,
        //        UseJitter = true,
        //        Delay = TimeSpan.FromSeconds(0.5)
        //    });

        //    pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        //    {
        //        SamplingDuration = TimeSpan.FromSeconds(10),
        //        FailureRatio = 0.9,
        //        MinimumThroughput = 5,
        //        BreakDuration = TimeSpan.FromSeconds(5)
        //    });

        //    pipeline.AddTimeout(TimeSpan.FromSeconds(1));
        //});

        // Encryption

        builder.Services.Configure<EncryptionOptions>
            (builder.Configuration.GetSection(EncryptionOptions.SectionName));

        builder.Services.AddTransient<EncryptionService>();

        // GitHub Automation

        builder.Services.Configure<GitHubAutomationOptions>
            (builder.Configuration.GetSection(GitHubAutomationOptions.SectionName));

        // Add ETag store

        builder.Services.AddSingleton<InMemoryETagStore>();

        return builder;
    }

    public static WebApplicationBuilder AddAuthenticationServices(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationIdentityDbContext>();

        builder.Services.Configure<JwtAuthOptions>(builder.Configuration.GetSection("Jwt"));

        JwtAuthOptions jwtAuthOptions = builder.Configuration.GetSection("Jwt").Get<JwtAuthOptions>()!;

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtAuthOptions.Issuer,
                    ValidAudience = jwtAuthOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtAuthOptions.Key))
                };
            });

        builder.Services.AddAuthorization();

        return builder;
    }

    public static WebApplicationBuilder AddBackgroundJobs(this WebApplicationBuilder builder)
    {
        builder.Services.AddQuartz(q =>
        {
            q.AddJob<GitHubAutomationSchedulerJob>(options => options.WithIdentity("github-automation-scheduler"));

            q.AddTrigger(option => option
                .ForJob("github-automation-scheduler")
                .WithIdentity("github-automation-scheduler-trigger")
                .WithSimpleSchedule(schedule =>
                {
                    GitHubAutomationOptions settings = builder.Configuration
                        .GetSection(GitHubAutomationOptions.SectionName)
                        .Get<GitHubAutomationOptions>()!;

                    schedule.WithIntervalInMinutes(settings.ScanIntervalInMinutes)
                        .RepeatForever();
                })
            );
        });

        builder.Services.AddQuartzHostedService(options =>
            options.WaitForJobsToComplete = true);

        return builder;
    }

    public static WebApplicationBuilder AddCorsPolicy(this WebApplicationBuilder builder)
    {
        CorsOptions corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()!;

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsOptions.PolicyName, policy =>
            {
                policy.WithOrigins(corsOptions.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return builder;
    }

    public static WebApplicationBuilder AddRateLimiter(this WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = $"{retryAfter.TotalSeconds}";

                    ProblemDetailsFactory problemDetailsFactory = context.HttpContext.RequestServices
                        .GetRequiredService<ProblemDetailsFactory>();

                    Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails = problemDetailsFactory
                        .CreateProblemDetails(
                            context.HttpContext,
                            StatusCodes.Status429TooManyRequests,
                            title: "Too Many Requests",
                            detail: $"Too many requests. Please try again in {retryAfter.TotalSeconds} seconds.");

                    await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: token);
                }
            };

            options.AddPolicy("default", httpContext =>
            {
                //string identityId = httpContext.User.Identity?.Name ?? string.Empty; // TO TEST RATE LIMITING
                string identityId = httpContext.User.GetIdentityId() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(identityId))
                {
                    return RateLimitPartition.GetTokenBucketLimiter(
                        identityId,
                        _ =>
                            new TokenBucketRateLimiterOptions
                            {
                                TokenLimit = 100,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = 5,
                                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                                TokensPerPeriod = 25
                            });
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    "anonymous",
                    _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1)
                    });
            });
        });

        return builder;
    }
}
