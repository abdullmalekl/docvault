# ============================================================================
# DocVault Enterprise - Dockerfile
# تثبيت النظام على Docker للإنتاج
# ============================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

# نسخ ملف المشروع
COPY DocVault.csproj .
RUN dotnet restore

# نسخ الملفات
COPY . .

# بناء المشروع
RUN dotnet publish -c Release -o out

# ============================================================================
# مرحلة التشغيل (Runtime)
# ============================================================================

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# تثبيت SQL Server Client (اختياري)
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# نسخ الملفات المُترجمة من مرحلة البناء
COPY --from=build /app/out .

# فتح المنفذ
EXPOSE 5000 5001

# متغيرات البيئة
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# الأمر الافتراضي
ENTRYPOINT ["dotnet", "DocVault_Demo_Complete.dll"]

# ============================================================================
# التعليقات:
# - يستخدم Multi-stage build لتقليل حجم الصورة
# - يشغل التطبيق على المنفذ 5000
# - جاهز للإنتاج
# ============================================================================
