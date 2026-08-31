# Backend — Gym Management SaaS

Stack: **.NET 8 (Web API) + SQL Server + Docker**

---

## 0. قواعد صارمة (Rules) — اقرأها قبل أي سطر كود

1. **Clean Architecture إجباري**: `Domain` مايعرفش حاجة عن `Application`. `Application` مايعرفش حاجة عن `Infrastructure` أو `API` (كله عن طريق Interfaces). ممنوع أي `using` عكسي.
2. **Multi-tenancy إجباري من أول سطر**: كل Entity تخص منشأة لازم فيها `FacilityId`. أي query لازم يتفلتر أوتوماتيك بالـ `FacilityId` بتاع التوكن الحالي (Global Query Filter في EF Core) — مش يعتمد على كل Handler يفتكر يفلتر يدوي.
3. **الـ `facility_id` بييجي من التوكن بس، مش من الـ request body/query string أبدًا.** لو جه من مصدر تاني، الـ request يترفض.
4. **Vertical Slice جوا Application**: كل Feature (Command/Query) في فولدر واحد بكل ملفاته، مش متوزع بين طبقات.
5. **كل Command له Validator منفصل (FluentValidation)** بيشتغل قبل الـ Handler تلقائيًا عن طريق Pipeline Behavior.
6. **مفيش try/catch جوا الـ Controllers.** كل الأخطاء بتتلقط مركزي في `ExceptionHandlingMiddleware`.
7. **الـ Audit Log بيتسجل أوتوماتيك** عن طريق `SaveChangesInterceptor`، مش عن طريق كل Handler يكتب سطر لوج يدوي.
8. **ممنوع تسجيل كلمات السر أو التوكنات أو بيانات العقد/الدفع في أي log** (حتى الـ Audit Log نفسه — لو حقل حساس اتغير، يتسجل "تم التعديل" بس مش القيمة).
9. **كل Entity حساسة (Facility, Subscription, PlatformSubscription) لازم فيها RowVersion column** لمنع تعارض التعديلات بصمت.
10. **أي endpoint خاص بالسوبرفايزر، لو المنشأة `LicenseType == Sold`، يرجع 404 مش 403.** الفرق مهم — 403 بيقول "فيه حاجة موجودة بس ممنوع عليك"، وده بيكشف وجود نظام سوبرفايزر أصلاً.
11. **استخدم `EnableRetryOnFailure()` في EF Core** — الـ SQL Server container ممكن ياخد وقت يبقى جاهز، مايتفشلش الـ API بسبب توقيت.
12. لو فيه أي Skill أو أداة متاحة عندك دلوقتي بتساعد في السكافولدينج، الاختبارات، أو التوثيق التقني للـ .NET — استخدمها، ما تشتغلش يدوي وهي موجودة.

---

## 1. هيكل المشروع

```
backend/
├── src/
│   ├── GymSaaS.Domain/
│   │   ├── Entities/
│   │   ├── Enums/
│   │   ├── Interfaces/
│   │   └── Exceptions/
│   ├── GymSaaS.Application/
│   │   ├── Common/{Behaviors, Interfaces, Mappings}/
│   │   └── Features/{Facilities, Supervisor, AuditLog, Contracts, Owners, Branches, Players, Payments}/
│   │       └── each feature: Commands/<Name>/{Command, Handler, Validator}, Queries/<Name>/
│   ├── GymSaaS.Infrastructure/
│   │   ├── Persistence/{DbContext, Configurations, Repositories, Interceptors, Migrations}/
│   │   ├── Identity/{JwtTokenGenerator, ImpersonationTokenService}/
│   │   └── Services/{PdfExportService}/
│   └── GymSaaS.API/
│       ├── Controllers/
│       ├── Middleware/{ExceptionHandling, TenantResolution, ImpersonationGuard, CorrelationId}/
│       └── Program.cs
├── tests/
│   ├── GymSaaS.Domain.UnitTests/
│   ├── GymSaaS.Application.UnitTests/
│   └── GymSaaS.API.IntegrationTests/
├── docker-compose.yml
├── Dockerfile
└── GymSaaS.sln
```

---

## 2. الـ Roles والـ Entities الأساسية

- **Supervisor** — حساب واحد بس في كل النظام (لا حاجة لجدول permissions معقد)
- **Owner** ← منشأة واحدة بالظبط (لا multi-facility ownership)
- **BranchManager** ← فرع واحد أو أكتر داخل نفس المنشأة
- **Coach**, **Reception** ← مرتبطين بفرع
- **Player** ← مرتبط بمنشأة + اشتراك

Entities رئيسية: `Facility`, `Branch`, `Owner`, `BranchManager`, `Coach`, `Receptionist`, `Player`, `Subscription` (اللاعب), `PlatformSubscription` (اشتراك المنشأة عند السوبرفايزر), `AuditLogEntry`, `Contract`, `ContractApproval`, `PaymentRecord`.

---

## 3. نظام السوبرفايزر (Supervisor) — المواصفات الكاملة

### 3.1 Role Switching / Impersonation
- الدخول على أي منشأة: اختيار Role (Owner/BranchManager/Coach/Reception) + فرع (لو منشأة متعددة الفروع)
- توكن الـ impersonation **منفصل** عن توكن الدخول العادي، عمره **30-60 دقيقة**، يحتاج تجديد بعدها
- التوكن فيه claims: `actor_type=SUPERVISOR`, `on_behalf_of_role`, `facility_id`, `supervisor_id`
- لو التوكن خلص وقت الاستخدام: رجّع `401` مع `error_code: IMPERSONATION_EXPIRED` (الفرونت يفتح مودال تجديد، مش يرمي السوبرفايزر برة)
- **مفيش أي إشعار بيوصل للمنشأة** وقت دخول السوبرفايزر أو بعده — الشفافية الوحيدة هي الـ Audit Log
- أي حركة كتابة (create/update/delete) خلال سيشن الـ impersonation تتسجل في الـ Audit Log بـ `performed_by = supervisor_id` و`on_behalf_of_role`

### 3.2 Audit Log
- كل سطر: `actor_id`, `actor_type`, `on_behalf_of_role`, `action_type` (create/update/delete), `entity_type`, `entity_id`, `old_value`, `new_value`, `timestamp`, `facility_id`, `branch_id`
- **Immutable تمامًا** — لا حذف ولا أرشفة يدوية من أي حد، ولا حتى الأونر
- الوصول: Owner (كل فروعه) ← BranchManager (فرعه بس) ← Coach/Reception (مفيش وصول خالص) ← Supervisor (كل حاجة)
- **الأداء على المدى الطويل**: اللوجات تفضل في جدول نشط لآخر 3 شهور، وبعدها **Hangfire recurring job** (شهري مثلاً) ينقلها لجدول أرشيف منفصل (يفضل قابل للبحث، بس مش هو الافتراضي اللي بيتحمل). **Hangfire مطلوب من أول سطر كود في المشروع** (مش إضافة لاحقة) — نفس الـ dashboard بتاعه ممكن يتستخدم كمان لتنبيهات انتهاء الاشتراكات (§3.3) ومهام دورية تانية مستقبلية

### 3.3 القفل/التجميد + الاشتراكات
- مستويين قفل مستقلين: (أ) المنصة كلها، (ب) فيتشرز إضافية منفصلة (Online Store, AI Coach)
- كل واحد له حالة مستقلة: `Active / ExpiringSoon / Expired / Frozen`
- تنبيه للأونر قبل الانتهاء بفترة (مثلاً 7 أيام)
- **تنبيه للسوبرفايزر نفسه** كمان قبل انتهاء أي اشتراك — مش مجرد اعتماد على مراجعة يدوية للوج
- **الدفع بالكامل أوفلاين** (تحويل بنكي مباشر للسوبرفايزر) — لا يوجد payment gateway في السيستم
- **فك القفل يدوي بالكامل** من السوبرفايزر بعد تأكيد استلام الفلوس — لا يوجد فك تلقائي
- عند القفل: **قطع فوري** لأي سيشن شغالة (لا يُسمح بإنهاء العملية الحالية)
- عند القفل الكامل: **كل الأدوار بما فيهم اللاعبين (Users) يتقفلوا معاه** — لا استثناء لأي دور
- **سجل مدفوعات داخلي**: كل مرة السوبرفايزر يستلم دفعة ويفك قفل، يتسجل (المبلغ، التاريخ، المنشأة)

### 3.4 خطط أسعار الفيتشرز الإضافية (Add-on Pricing Plans)
- السوبرفايزر هو الوحيد اللي يقدر يعرّف/يعدّل خطط الأسعار الخاصة بالفيتشرز المدفوعة الإضافية (زي Online Store, AI Coach) — Entity منفصل: `AddOnFeature` (الاسم، الوصف، السعر، هل نشط للبيع أصلاً)
- كل منشأة عندها جدول ربط `FacilityAddOnSubscription` يوضح: أي فيتشرز مفعّلة عندها، تاريخ التفعيل، وحالتها (Active/Frozen) — مستقلة تمامًا عن حالة الاشتراك الأساسي في المنصة (§3.3)
- لما السوبرفايزر يفعّل فيتشر إضافي لمنشأة معينة (بعد استلام دفعة أوفلاين زي أي دفعة تانية)، الحركة دي:
  - تتسجل في **سجل المدفوعات الداخلي** (نفس الموصوف في §3.3) بنفس الشكل: المبلغ، التاريخ، المنشأة، ونوع العملية (اشتراك أساسي / فيتشر إضافي)
  - **تنعكس فورًا في شاشة/تقرير إيرادات السوبرفايزر** (Query منفصل: `GetSupervisorRevenueOverview`) — يفصل بين إيرادات الاشتراكات الأساسية وإيرادات الفيتشرز الإضافية، عشان السوبرفايزر يشوف مصدر كل جنيه
- **لا ينطبق على منشآت `LicenseType == Sold`** — الليسنس المبيعة شاملة كل الفيتشرز من الأول (§3.5)، فمفيش لها سجل `FacilityAddOnSubscription` منفصل أصلاً

### 3.5 خيار "بيع السيستم" (Sold License)
- يُحدَّد وقت إنشاء المنشأة: `LicenseType = Sold` أو `Subscription`
- **مدة الليسنس تُختار لكل منشأة على حدة** (Lifetime بدون انتهاء، أو مدة محددة) — مش قاعدة عامة ثابتة
- الليسنس المبيعة = **كل حاجة شاملة من الأول** — لا فيتشرز إضافية مدفوعة تُباع فوقها لاحقًا
- **إخفاء تام**: مفيش أي أثر لأي endpoint خاص بالسوبرفايزر لمنشأة `Sold` — يرجع `404` مش `403`

### 3.6 أمان حساب السوبرفايزر
- **حساب واحد بس** في كل النظام (ممكن يتشارك مع شريك، لا حاجة لنظام صلاحيات فريق)
- **2FA إجباري** على هذا الحساب تحديدًا (أعلى أولوية أمان في كل السيستم)
- **الطريقة: TOTP عن طريق Google Authenticator** (وليس SMS أو Email OTP) — استخدم مكتبة `Otp.NET` لتوليد الـ secret وQR code وقت الإعداد، والتحقق من الكود وقت الدخول. الـ secret يُخزَّن مشفّرًا في الـ DB، لا يُخزَّن أو يُرسل نص صريح أبدًا

### 3.7 العقد الإلكتروني والتوقيع
- عند أول تفعيل لحساب Owner (قبل الـ Onboarding): يظهر نص العقد كامل (نص عادي/HTML جوا الصفحة، **ليس PDF مرفوع**)
- موافقة صريحة: checkbox إجباري + زرار "أوافق وأقر"
- **التوقيع**: الأونر يكتب اسمه الكامل، والسيستم يعرضه بخط يد مولّد أوتوماتيكيًا (handwriting font) كتوقيع
  - **الخط المستخدم**: `Aref Ruqaa` (خط الرقعة العربي من Google Fonts) للأسماء بالعربي — أقرب خط مجاني لشكل التوقيع الحقيقي بالعربي لأنه مبني أصلاً على خط الرقعة المستخدم في التوقيعات والكتابة اليومية السريعة، مش خط زخرفي. لو الاسم اتكتب بالإنجليزي، استخدم `Dancing Script` كبديل. الخطين الاتنين مجانيين ومرخصين للاستخدام التجاري (SIL Open Font License)
- يُسجَّل: IP، تاريخ/وقت دقيق، **نسخة العقد (version)** الموافَق عليها
- **جدول عقود مُنسخن (versioned)** — مش نص ثابت بالكود — لو العقد اتغير، الأونر **لازم يوافق من جديد** على النسخة الجديدة (الموافقة القديمة لا تُحتسب)
- منع تام من الوصول لأي صفحة تانية قبل التوقيع
- إمكانية **تصدير PDF** للعقد الموقّع من صفحة تفاصيل المنشأة عند السوبرفايزر
- **إشعار للسوبرفايزر فور توقيع أي أونر على العقد** (نفس آلية تنبيهات §3.3 — إشعار داخل الداشبورد على الأقل)
- **نسخة احتياطية منفصلة من العقد الموقّع** تُحفظ تلقائيًا وقت التوقيع (مثلاً: نسخة PDF تتولّد وتتخزن في storage منفصل عن جدول `ContractApproval` نفسه) — ضمان لو حصل أي فقدان أو تلف في البيانات الأساسية، يفضل عندك نسخة مستقلة تقدر ترجعلها

### 3.8 دعم وإلغاء
- **لا يوجد إلغاء ذاتي (self-serve cancellation)** — الأونر يتواصل مع السوبرفايزر مباشرة، والإلغاء يتم من طرف السوبرفايزر فقط
- **نظام تيكت/دعم داخل الداشبورد** (وليس قناة خارجية زي واتساب) للأونر للتواصل مع السوبرفايزر
- **الحذف النهائي للبيانات يدوي بالكامل** من السوبرفايزر، بدون أي auto-purge بعد فترة زمنية

### 3.9 التشغيل الأول (Bootstrap) وحماية الدخول
- **أول حساب Supervisor** يتزرع عن طريق **EF Core Migration Seed ثابت** وقت أول تشغيل للنظام (`HasData` أو `Migration.Up()` مخصصة) — مش من `appsettings.json` أو `.env`، عشان يبقى جزء من تاريخ الـ DB نفسه ومايتفقدش لو الإعدادات اتغيرت
  - الباسورد الابتدائي يتغير إجباريًا أول ما يدخل (flag `MustChangePassword` على الحساب)
- **حماية من محاولات الدخول المتكررة (Brute Force)** على تسجيل دخول Supervisor وOwner معًا:
  - Rate limiting على endpoint اللوجين نفسه باستخدام مكتبة **`AspNetCoreRateLimit`** (قرار نهائي — مكتبة ناضجة ومدعومة، بتغطي الحد بالـ IP والحد بالحساب مع بعض) — عدد محاولات محدود لكل IP/حساب خلال فترة زمنية
  - بعد عدد معيّن من المحاولات الفاشلة المتتالية على نفس الحساب، **قفل مؤقت (account lockout)** لفترة تصاعدية، مش قفل نهائي يحتاج تدخل يدوي كل مرة
  - كل محاولة فاشلة تتسجل (بدون كلمة السر المُدخلة بالطبع) لأغراض المراقبة الأمنية

---
- بعد التوقيع على العقد مباشرة: صفحة Onboarding لإكمال بيانات المنشأة (الاسم، الفروع، إلخ) قبل الوصول للداشبورد
- Flag على حساب الأونر: `OnboardingCompleted: false` حتى يخلص الفورم

---

## 5. Error Handling (الأهم — نفّذها كاملة، بدون اختصار)

### 5.1 Exceptions المطلوبة (Domain layer)
```
DomainException (الأب)
├── ValidationException       → 400
├── NotFoundException         → 404
├── ConflictException         → 409
├── ForbiddenAccessException  → 403
└── FacilityLockedException   → 423
```

### 5.2 التدفق
1. **FluentValidation** على كل Command — يشتغل عن طريق MediatR Pipeline Behavior قبل الـ Handler، يرجع كل أخطاء الحقول مرة واحدة
2. **ExceptionHandlingMiddleware مركزي** — كل الأخطاء تتلقط هنا، ترجع بشكل موحّد:
```json
{
  "success": false,
  "statusCode": 400,
  "message": "...",
  "errors": { "email": ["..."] },
  "correlationId": "..."
}
```
3. **401 vs 403 لازم يتفرقوا بدقة**: 401 = توكن غير صالح/منتهي (الفرونت يودي للوجين)، 403 = توكن صالح بس صلاحية ناقصة (الفرونت يوري رسالة بس من غير logout)
4. **توكن impersonation منتهي**: كود مخصص `401 + IMPERSONATION_EXPIRED` (سلوك فرونت مختلف عن 401 عادي)
5. **الخطأ 500 لا يرجع تفاصيل حقيقية للمستخدم أبدًا** (لا stack trace ولا رسائل SQL) — فقط `correlationId` يربط بالـ logs الكاملة (Serilog)
6. **DbUpdateConcurrencyException** (تعارض تعديلات) → `409` مع رسالة "البيانات اتغيرت، حدّث الصفحة" — يتطلب `RowVersion` على الـ entities الحساسة
7. **فشل اتصال DB بعد retries** → `503` مش `500`
8. **Idempotency-Key header** على العمليات الحساسة (فك القفل، إنشاء منشأة) لمنع التكرار عند دبل-كليك أو انقطاع نت
9. **Correlation ID** يُنشأ في أول Middleware لكل request، يُرفق بكل سطر log وبكل error response

### 5.3 اختبار الأخطاء (إجباري لكل Feature)
كل Feature في `Application.UnitTests` لازم يغطي 4 حالات: بيانات غلط، صلاحية ناقصة، منشأة مقفولة، الحالة الناجحة. لا يُعتبر الـ Feature مكتمل بدون الأربعة.

---

## 6. Docker

```yaml
services:
  api:
    build: ./backend
    depends_on:
      db:
        condition: service_healthy
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    healthcheck:
      test: ["CMD", "/opt/mssql-tools/bin/sqlcmd", "-U", "sa", "-P", "$SA_PASSWORD", "-Q", "SELECT 1"]
      interval: 10s
      retries: 10
```
- **كل الأسرار من `.env`**، ممنوع أي secret يتكتب في الكود أو الـ docker-compose مباشرة
- `.gitignore` يشمل `.env` من أول commit

---

## 7. أوامر البداية المتوقعة
```
dotnet new sln -n GymSaaS
dotnet new classlib -n GymSaaS.Domain -o src/GymSaaS.Domain
dotnet new classlib -n GymSaaS.Application -o src/GymSaaS.Application
dotnet new classlib -n GymSaaS.Infrastructure -o src/GymSaaS.Infrastructure
dotnet new webapi -n GymSaaS.API -o src/GymSaaS.API
# + إضافة References بينهم حسب اتجاه الاعتماد في القسم 1
```

### مكتبات إضافية مؤكدة (نهائية، مش اختيارية)
```
dotnet add src/GymSaaS.Infrastructure package Hangfire.Core
dotnet add src/GymSaaS.Infrastructure package Hangfire.SqlServer
dotnet add src/GymSaaS.API package Hangfire.AspNetCore
dotnet add src/GymSaaS.Infrastructure package Otp.NET               # Google Authenticator (TOTP)
dotnet add src/GymSaaS.Infrastructure package QRCoder                # توليد QR code للـ 2FA setup
dotnet add src/GymSaaS.API package AspNetCoreRateLimit                # Brute force protection
```
