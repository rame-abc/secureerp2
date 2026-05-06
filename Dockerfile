# 🚀 SECUREERP2 PRODUCTION DOCKERFILE - RENDER DEPLOYMENT
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 10000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["SecureERP2.csproj", "./"]
RUN dotnet restore "./SecureERP2.csproj"

COPY . .
WORKDIR "/src/."

# Build only production output (skip unnecessary parts)
RUN dotnet publish SecureERP2.csproj -c Release -o /app/out --no-restore

FROM base AS final
WORKDIR /app

COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "SecureERP2.dll"]
