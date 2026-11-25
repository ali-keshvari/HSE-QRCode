# سامانه پویا و هوشمند بازرسی تجهیزات و رهگیری با QR Code (HSE-QRCode)

سیستم جامع ثبت بازرسی تجهیزات ایمنی و صنعتی (HSE) با قابلیت اتصال به SQL Viewهای موجود، تعریف فیلدهای سفارشی پویا، تولید شاخص یکتا و کد QR، چاپ برگه A4 و لیبل حرارتی، و پنل پرسنل مجاز.

## تکنولوژی‌ها
- **Backend:** ASP.NET Framework 4.8 (MVC 5 + Web API 2), C# 7.3
- **ORM & DB:** Entity Framework 6.2.0, Microsoft SQL Server
- **QR & Calendar:** `QRCoder`, `System.Globalization.PersianCalendar`
- **Frontend:** RTL Persian (`fa-IR`), Bootstrap 5 RTL, Vazirmatn, Select2, JalaliDatePicker

## ساختار سولوشن
- `HSEQRCode.Domain` — موجودیت‌ها، Enumها و DTOها
- `HSEQRCode.Data` — `HseDbContext` (EF 6.2.0) و کوئری‌های امن متادیتای View
- `HSEQRCode.Services` — سرویس‌های بازرسی View (EAV و Flat)، تولید QR/شاخص، تاریخ شمسی و دسترسی RBAC
- `HSEQRCode.Web` — کنترلرهای MVC 5 / Web API 2، ویوهای Razor راست‌به‌چپ و استایل‌های چاپ
- `HSEQRCode.Tests` — ۳۸ تست واحد و یکپارچه‌سازی (امنیت SQL، EAV، تاریخ شمسی، QR، RBAC و چاپ)
- `Database/` — اسکریپت‌های `01_Schema.sql` و `02_Seed_FireExtinguisher_1001.sql`

## راه‌اندازی سریع
1. اسکریپت‌های `Database/01_Schema.sql` و `Database/02_Seed_FireExtinguisher_1001.sql` را روی دیتابیس `HSEQRCode` در SQL Server اجرا کنید.
2. در صورت نیاز، رشته اتصال `HseDbConnection` را در `HSEQRCode.Web/Web.config` تنظیم کنید.
3. پروژه `HSEQRCode.Web` را در Visual Studio 2022 اجرا کنید.
   - مدیر پیش‌فرض: `admin` / `Admin@123`
   - بازرس پیش‌فرض: `inspector1` / `Inspector@123`
