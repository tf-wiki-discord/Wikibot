using FluentScheduler;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Wikibot.App.Data;
using Wikibot.App.Extensions;
using Wikibot.App.Models;
using Wikibot.DataAccess;
using Wikibot.Logic.JobRetrievers;
using Wikibot.Logic.Logic;
using Wikibot.Logic.UserRetrievers;

namespace Wikibot.App
{
    public class Startup
    {
        private const string SecretKey = "w3asefr9awesfW03F9UWEQFAQWE09FSU0WQE9FU"; // todo: get this from somewhere secure
        private readonly SymmetricSecurityKey _signingKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(SecretKey));

        public Startup(IConfiguration configuration)
        {

            Configuration = configuration;
            JobManager.Initialize(new Registry());
            JobManager.UseUtcTime();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // jwt wire up
            services.AddSingleton<IJwtFactory, JwtFactory>();
            
            // Get options from app settings
            var jwtAppSettingOptions = Configuration.GetSection(nameof(JwtIssuerOptions));

            // Configure JwtIssuerOptions
            services.Configure<JwtIssuerOptions>(options =>
            {
                options.Issuer = jwtAppSettingOptions[nameof(JwtIssuerOptions.Issuer)];
                options.Audience = jwtAppSettingOptions[nameof(JwtIssuerOptions.Audience)];
                options.SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
            });

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtAppSettingOptions[nameof(JwtIssuerOptions.Issuer)],

                ValidateAudience = true,
                ValidAudience = jwtAppSettingOptions[nameof(JwtIssuerOptions.Audience)],

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,

                RequireExpirationTime = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

            };

            services.AddAuthentication(options =>
            {
            }).AddJwtBearer(configureOptions =>
            {
                configureOptions.ClaimsIssuer = jwtAppSettingOptions[nameof(JwtIssuerOptions.Issuer)];
                configureOptions.TokenValidationParameters = tokenValidationParameters;
                configureOptions.SaveToken = true;
            });

            var builder = new SqlConnectionStringBuilder(
            Configuration.GetConnectionString("ApplicationDB"));
            builder.Password = Configuration.GetSection("ApplicationDB")["DbPassword"];

            //TODO: Add seperate connection string for this context
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                            builder.ConnectionString
                )
            ) ;

            // api user claim policy
            services.AddAuthorization(options =>
            {
                options.AddPolicy("BotAdmin", policy => {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimTypes.Role);
                    policy.RequireRole("BotAdmin");
                    policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
                    });
            });

            services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true).AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

            var identityBuilder = services.AddIdentityCore<ApplicationUser>(o =>
            {
                // configure identity options
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredLength = 6;
            });

            identityBuilder = new IdentityBuilder(identityBuilder.UserType, typeof(IdentityRole), identityBuilder.Services);
            identityBuilder.AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

            services.AddControllersWithViews();
            services.AddRazorPages();

            var logConfig = new LoggerConfiguration().ReadFrom.Configuration(Configuration);
            var logger = logConfig.CreateLogger();
            services.AddSingleton<Serilog.ILogger>(logger);

            //services.AddTransient<IEmailSender, EmailSender>();
            services.AddTransient<IWikiAccessLogic, WikiAccessLogic>();
            services.AddTransient<IWikiRequestRetriever, TFWikRequestRetriever>();
            services.AddTransient<IDataAccess, SqlDataAccess>();
            services.AddTransient<IUserRetriever, TFWikiUserRetriever>();

            services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Name = "Authorization",

                    //Flows = new OpenApiOAuthFlows
                    //{
                    //    Implicit = new OpenApiOAuthFlow
                    //    {
                    //        //AuthorizationUrl = new Uri("https://localhost:5001/api/authenticationcontroller/login"),
                    //        TokenUrl = new Uri("https://localhost:5001/api/authenticationcontroller/login"),
                    //        Scopes = new Dictionary<string, string>
                    //        {
                    //            {"api1", "Demo API - full access"}
                    //        }
                    //    }
                    //}
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "ApiKey"
                            },
                        },
                        new List<string>()
                    }
                });
                options.OperationFilter<AuthorizeCheckOperationFilter>();
            });
            //services.Configure<AuthMessageSenderOptions>(Configuration);

            services.AddCors(c =>
            {
                c.AddPolicy("AllowOrigin", options => options.WithOrigins(Configuration.GetValue<string>("AllowedDomains")));
                c.AddPolicy("AllowAllHeaders",
                    builder =>
                    {
                        builder.WithOrigins("http://localhost:4200")
                            .AllowAnyHeader();
                    });
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider, ILogger logger, IHostApplicationLifetime lifetime)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                //app.UseDatabaseErrorPage();
            }
            else
            {
                app.ConfigureExceptionHandler(logger);
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseCors(options => options.AllowAnyOrigin().AllowAnyHeader());
            //app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseSerilogRequestLogging();
 
            app.UseAuthentication();
            app.UseRouting();

            app.Use(async (context, next) =>
            {
                // Do work that doesn't write to the Response.
                await next.Invoke();
                // Do logging or other work that doesn't write to the Response.
            });


            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });

            // Check for lifetime shutdown working with WebSocket active
            lifetime.ApplicationStopping.Register(() =>
            {
                logger.Information("Wikibot is shutting down.");
            }, true);

            lifetime.ApplicationStopped.Register(() =>
            {
                logger.Information("Wikibot has stopped.");
            }, true);

            var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
            var dashboardUrl = configuration["DashboardURL"];
            app.Use(async (context, next) =>
            {
                var url = context.Request.Path.Value;

                // Redirect to an external URL
                if (url.Contains("/Dashboard") && (context.User.IsInRole("WikiMod") || context.User.IsInRole("BotAdmin")))
                {
                    var jwtOptions = app.ApplicationServices.GetRequiredService<IOptions<JwtIssuerOptions>>();
                    var factory = new JwtFactory(jwtOptions);
                    var jwtToken = await factory.GenerateEncodedToken(context.User.Identity.Name, context.User.Identities.First());
                    context.Response.Redirect($"{dashboardUrl}?auth_token= {jwtToken}"); //Update URL
                    return;   // short circuit
                }

                await next();
            });

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
            });
            
            CreateUserRoles(serviceProvider, configuration).Wait();
        }

        private async Task CreateUserRoles(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            var RoleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var UserManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            IdentityResult roleResult;
            //Adding Admin Role
            var roleCheck = await RoleManager.RoleExistsAsync("BotAdmin");
            if (!roleCheck)
            {
                //create the roles and seed them to the database
                roleResult = await RoleManager.CreateAsync(new IdentityRole("BotAdmin"));
            }

            roleCheck = await RoleManager.RoleExistsAsync("WikiMod");
            if (!roleCheck)
            {
                //create the roles and seed them to the database
                roleResult = await RoleManager.CreateAsync(new IdentityRole("WikiMod"));
            }
            //Assign Admin role to the main User here we have given our newly registered 
            //login id for Admin management
            var rootAdminAddress = configuration["RootAdminEmailAddress"];
            ApplicationUser user = await UserManager.FindByEmailAsync(rootAdminAddress);
            await UserManager.AddToRoleAsync(user, "BotAdmin");
        }
    }
}
