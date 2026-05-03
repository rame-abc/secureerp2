using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SecureERP2
{
    // 🔒 CORS Configuration for Vercel Frontend Integration
    public static class CorsConfiguration
    {
        public static void ConfigureCors(IServiceCollection services, IWebHostEnvironment environment)
        {
            // CORS policy for Vercel frontend
            services.AddCors(options =>
            {
                options.AddPolicy("AllowVercelFrontend", policy =>
                {
                    // Allow Vercel frontend
                    policy.WithOrigins(
                        "http://localhost:3000",           // Development
                        "http://localhost:3001",           // Development alternative
                        "https://erp-frontend.vercel.app",  // Production Vercel
                        "https://erp-frontend-*.vercel.app" // All Vercel preview deployments
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials(); // Important for JWT cookies/tokens
                });

                // Development CORS policy
                if (environment.IsDevelopment())
                {
                    options.AddPolicy("AllowDevelopment", policy =>
                    {
                        policy.WithOrigins(
                            "http://localhost:3000",
                            "http://localhost:3001",
                            "http://localhost:5173",  // Vite dev server
                            "http://127.0.0.1:3000",
                            "http://127.0.0.1:3001"
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                    });
                }
            });
        }

        public static void UseCorsConfiguration(IApplicationBuilder app, IWebHostEnvironment environment)
        {
            // Use appropriate CORS policy based on environment
            if (environment.IsDevelopment())
            {
                app.UseCors("AllowDevelopment");
            }
            else
            {
                app.UseCors("AllowVercelFrontend");
            }
        }
    }

    // 🔒 Production CORS Settings
    public static class ProductionCorsSettings
    {
        // Add these origins to your backend CORS policy
        public static readonly string[] AllowedOrigins = new[]
        {
            "https://erp-frontend.vercel.app",
            "https://erp-frontend-*.vercel.app", // Wildcard for preview deployments
            "https://your-custom-domain.com"       // Add your custom domain here
        };

        // CORS headers that should be allowed
        public static readonly string[] AllowedHeaders = new[]
        {
            "Authorization",
            "Content-Type",
            "X-Company-Id",
            "X-Requested-With",
            "Accept",
            "Origin"
        };

        // HTTP methods that should be allowed
        public static readonly string[] AllowedMethods = new[]
        {
            "GET",
            "POST",
            "PUT",
            "DELETE",
            "OPTIONS",
            "PATCH"
        };
    }
}
