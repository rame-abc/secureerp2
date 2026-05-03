# 🚀 STEP 25.4: Prepare Deployment - Docker Configuration
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SecureERP2.csproj", "./"]
RUN dotnet restore "./SecureERP2.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "SecureERP2.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SecureERP2.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SecureERP2.dll"]
