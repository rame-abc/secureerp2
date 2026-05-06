# 🚀 SECUREERP2 PRODUCTION DEPLOYMENT
param(
    [Parameter(Mandatory=$false)]
    [string]$Environment = "Production"
)

Write-Host "🔒 SECUREERP2 PRODUCTION DEPLOYMENT" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

# Step 1: Pre-deployment checks
Write-Host "📋 STEP 1: PRE-DEPLOYMENT CHECKS" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

if (-not (Test-Path "appsettings.Production.json")) {
    Write-Host "❌ ERROR: Production configuration file not found" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path "Dockerfile")) {
    Write-Host "❌ ERROR: Dockerfile not found" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Pre-deployment checks passed" -ForegroundColor Green
Write-Host ""

# Step 2: Build production image
Write-Host "🔨 STEP 2: BUILD PRODUCTION IMAGE" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

try {
    Write-Host "📦 Building production Docker image..." -ForegroundColor Cyan
    $buildResult = docker build -t secureerp2:production .
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Production image built successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ ERROR: Docker build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "❌ ERROR: Build process failed" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 3: Deploy to production
Write-Host "🚀 STEP 3: DEPLOY TO PRODUCTION" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Yellow

try {
    Write-Host "🛑 Stopping existing container..." -ForegroundColor Cyan
    docker stop secureerp2-production 2>$null
    docker rm secureerp2-production 2>$null
    
    Write-Host "🚀 Starting production container..." -ForegroundColor Cyan
    $deployResult = docker run -d --name secureerp2-production --restart unless-stopped -p 5000:5000 -p 5001:5001 -e ASPNETCORE_ENVIRONMENT=Production -e ConnectionStrings__DefaultConnection="Server=production-db-host;Database=SecureERP2_Production;User Id=secureerp_user;Password=PROD_PASSWORD_PLACEHOLDER;TrustServerCertificate=true;Encrypt=true;" -e Authentication__Jwt__SecretKey="PROD_JWT_SECRET_KEY_PLACEHOLDER_MINIMUM_32_CHARACTERS" -v /c/data/secureerp2/logs:/app/logs -v /c/data/secureerp2/backups:/app/backups --memory 2g --cpus 1.0 secureerp2:production
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Production deployment completed successfully" -ForegroundColor Green
        
        Write-Host "⏳ Waiting for service to be ready..." -ForegroundColor Cyan
        Start-Sleep -Seconds 30
        
        # Health check after deployment
        $attempts = 0
        $maxAttempts = 10
        
        do {
            $attempts++
            Write-Host "🔍 Health check attempt $attempts of $maxAttempts..." -ForegroundColor Cyan
            
            try {
                $response = Invoke-RestMethod -Uri "http://localhost:5000/health" -Method GET -TimeoutSec 10
                if ($response.StatusCode -eq 200) {
                    Write-Host "✅ Service is healthy and ready" -ForegroundColor Green
                    break
                }
            }
            catch {
                Write-Host "⚠️ Health check failed, retrying..." -ForegroundColor Yellow
            }
            
            if ($attempts -lt $maxAttempts) {
                Start-Sleep -Seconds 5
            }
        } while ($attempts -lt $maxAttempts)
        
        if ($attempts -eq $maxAttempts) {
            Write-Host "❌ ERROR: Service failed to become healthy after $maxAttempts attempts" -ForegroundColor Red
            exit 1
        }
        
        # Display deployment summary
        Write-Host ""
        Write-Host "📊 DEPLOYMENT SUMMARY" -ForegroundColor Green
        Write-Host "==========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "🌐 Production URL: http://localhost:5000" -ForegroundColor Cyan
        Write-Host "🏥 Health Endpoint: http://localhost:5000/health" -ForegroundColor Cyan
        Write-Host "📈 Metrics Endpoint: http://localhost:5000/metrics" -ForegroundColor Cyan
        Write-Host "📊 Analytics Endpoint: http://localhost:5000/api/analytics" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "🎯 SECUREERP2 IS NOW RUNNING IN PRODUCTION!" -ForegroundColor Green
        Write-Host "==========================================" -ForegroundColor Green
        
    } else {
        Write-Host "❌ ERROR: Docker deployment failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "❌ ERROR: Deployment process failed" -ForegroundColor Red
    exit 1
}
