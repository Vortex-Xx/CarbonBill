# =========================================================
# Multi-stage Dockerfile for CarbonBill (API + Hangfire Worker)
# Includes Tesseract OCR binaries and Bangla/English traineddata
# =========================================================

# Stage 1: Build stage (.NET 8 SDK)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy Directory.Build.props and solution
COPY Directory.Build.props ./
COPY CarbonBill.sln ./

# Copy all csproj files for layer caching
COPY src/CarbonBill.SharedKernel/CarbonBill.SharedKernel.csproj src/CarbonBill.SharedKernel/
COPY src/CarbonBill.Modules.IdentityTenancy/CarbonBill.Modules.IdentityTenancy.csproj src/CarbonBill.Modules.IdentityTenancy/
COPY src/CarbonBill.Modules.Audit/CarbonBill.Modules.Audit.csproj src/CarbonBill.Modules.Audit/
COPY src/CarbonBill.Modules.Onboarding/CarbonBill.Modules.Onboarding.csproj src/CarbonBill.Modules.Onboarding/
COPY src/CarbonBill.Modules.Documents/CarbonBill.Modules.Documents.csproj src/CarbonBill.Modules.Documents/
COPY src/CarbonBill.Modules.Extraction/CarbonBill.Modules.Extraction.csproj src/CarbonBill.Modules.Extraction/
COPY src/CarbonBill.Modules.Review/CarbonBill.Modules.Review.csproj src/CarbonBill.Modules.Review/
COPY src/CarbonBill.Modules.ActivityUnits/CarbonBill.Modules.ActivityUnits.csproj src/CarbonBill.Modules.ActivityUnits/
COPY src/CarbonBill.Modules.FactorRegistry/CarbonBill.Modules.FactorRegistry.csproj src/CarbonBill.Modules.FactorRegistry/
COPY src/CarbonBill.Modules.Calculation/CarbonBill.Modules.Calculation.csproj src/CarbonBill.Modules.Calculation/
COPY src/CarbonBill.Modules.GapDetection/CarbonBill.Modules.GapDetection.csproj src/CarbonBill.Modules.GapDetection/
COPY src/CarbonBill.Modules.Flags/CarbonBill.Modules.Flags.csproj src/CarbonBill.Modules.Flags/
COPY src/CarbonBill.Modules.Recommendations/CarbonBill.Modules.Recommendations.csproj src/CarbonBill.Modules.Recommendations/
COPY src/CarbonBill.Modules.Reporting/CarbonBill.Modules.Reporting.csproj src/CarbonBill.Modules.Reporting/
COPY src/CarbonBill.Modules.Notifications/CarbonBill.Modules.Notifications.csproj src/CarbonBill.Modules.Notifications/
COPY src/CarbonBill.Modules.PlatformAdmin/CarbonBill.Modules.PlatformAdmin.csproj src/CarbonBill.Modules.PlatformAdmin/
COPY src/CarbonBill.Modules.Insights/CarbonBill.Modules.Insights.csproj src/CarbonBill.Modules.Insights/
COPY src/CarbonBill.Api/CarbonBill.Api.csproj src/CarbonBill.Api/

RUN dotnet restore src/CarbonBill.Api/CarbonBill.Api.csproj

# Copy all source files and publish
COPY src/ src/
RUN dotnet publish src/CarbonBill.Api/CarbonBill.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime image with Tesseract OCR (ben + eng)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install Tesseract OCR and language packs (English + Bangla)
RUN apt-get update && apt-get install -y --no-install-recommends \
    tesseract-ocr \
    tesseract-ocr-eng \
    tesseract-ocr-ben \
    libgdiplus \
    sqlite3 \
    && rm -rf /var/lib/apt/lists/*

# Create persistent data volume directory
RUN mkdir -p /data && chmod 777 /data
VOLUME /data

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ConnectionStrings__DefaultConnection="Data Source=/data/carbonbill.db;Cache=Shared" \
    ConnectionStrings__HangfireConnection="Data Source=/data/hangfire.db;Cache=Shared"

EXPOSE 5000

ENTRYPOINT ["dotnet", "CarbonBill.Api.dll"]
