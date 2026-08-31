# GymSaaS API Contract Documentation

توصيف عقد الخدمات التكاملي (API Contract) للربط بين Frontend و Backend لنظام GymSaaS.

---

## 1. الهيدرز الأساسية (Headers)

- `Authorization: Bearer <Token>` — مطلوب لجميع الـ Endpoints المحمية.
- `Idempotency-Key: <UUID>` — مطلوب في العمليات الحساسة لمنع التكرار عند دبل-كليك أو انقطاع الاتصال (مثال: فك القفل `POST /api/facilities/{id}/unlock` وتفعيل الـ Add-ons `POST /api/addons/activate`).
- `X-Correlation-ID` — اختياري من الفرونت، يتم إرجاعه دائماً من الباك اند لربط الـ Logs والـ Errors.

---

## 2. التوكنات (Tokens): فرق الدخول العادي عن الـ Impersonation

| نوع التوكن | المصدر | الصلاحية | الاستخدام |
|-----------|--------|---------|-----------|
| **Normal JWT** | `POST /api/auth/login/supervisor` أو `POST /api/auth/login/owner` | 7 أيام | الدخول الطبيعي للداشبورد |
| **Impersonation Token** | `POST /api/auth/impersonate` | 30–60 دقيقة | دخول السوبرفايزر بصفة Role معينة داخل منشأة |

### كيف يفرق الفرونت بينهما؟
التوكن المنكّر (Impersonation Token) يحتوي على الـ Claims التالية:
- `is_impersonating: true`
- `on_behalf_of_role: Owner | BranchManager | Coach | Receptionist`
- `supervisor_id: <ID>`

عند انتهاء توكن الـ Impersonation خلال الاستخدام، يرجع الباك إند:
`HTTP 401 Unauthorized` مع `errorCode: "IMPERSONATION_EXPIRED"` — **يجب على الفرونت فتح المودال الخاص بتجديد التوكن بدلاً من تسجيل الخروج.**

---

## 3. الهيكل الموحد للأخطاء (Standardized Error Response)

جميع الأخطاء ترجع بالشكل الموحد التالي:

```json
{
  "success": false,
  "statusCode": 400,
  "errorCode": "OPTIONAL_ERROR_CODE",
  "message": "نص الخطأ بالعربي ليتم عرضه للمستخدم",
  "errors": {
    "email": ["البريد الإلكتروني غير صحيح."]
  },
  "correlationId": "a1b2c3d4e5f6"
}
```

### قائمة الـ Error Codes المخصصة وتوجيهات الفرونت:

| HTTP Status | Error Code | المعنى | إجراء الفرونت المطلوب |
|-------------|------------|--------|----------------------|
| `401` | `IMPERSONATION_EXPIRED` | جلسة دخول السوبرفايزر المنكّرة انتهت | فتح مودال تجديد التوكن السريع بدون Logout |
| `401` | — | التوكن العادي انتهى أو غير صالح | توجيه لصفحة تسجيل الدخول (Logout) |
| `403` | — | صلاحيات غير كافية | عرض رسالة تنبيه للمستخدم دون تسجيل الخروج |
| `404` | — | المورد غير موجود (أو منشأة `Sold`) | عرض صفحة Not Found أو رسالة عدم وجود المورد |
| `409` | `CONCURRENCY_CONFLICT` | تعارض تعديلات (Data modified by another user) | عرض تنبيه "البيانات تم تعديلها، يرجى تحديث الصفحة" |
| `423` | `FACILITY_LOCKED` | المنشأة مجمّدة بسبب عدم الدفع | عرض شاشة التجميد وإشعار الأونر للتواصل مع الإدارة |
| `503` | — | تعذر الاتصال بقاعدة البيانات | عرض رسالة "الخدمة غير متاحة حالياً، حاول لاحقاً" |

---

## 4. قائمة الـ Endpoints والـ Request/Response Shapes

---

### 4.1 Auth & Security (`/api/auth`)

#### `POST /api/auth/login/supervisor`
تسجيل دخول السوبرفايزر (المرحلة الأولى: البريد وكلمة السر).

- **Request Body:**
```json
{
  "email": "admin@gymsaas.com",
  "password": "Password123!"
}
```

- **Response (إعداد 2FA لأول مرة):**
```json
{
  "success": true,
  "data": {
    "token": null,
    "requiresTotpSetup": true,
    "requiresTotpVerification": false,
    "totpSetupQrUri": "data:image/png;base64,...",
    "temporaryToken": "BASE32_SECRET",
    "mustChangePassword": true
  }
}
```

- **Response (الحساب مفعّل 2FA):**
```json
{
  "success": true,
  "data": {
    "token": null,
    "requiresTotpSetup": false,
    "requiresTotpVerification": true,
    "totpSetupQrUri": null,
    "temporaryToken": "admin@gymsaas.com",
    "mustChangePassword": false
  }
}
```

---

#### `POST /api/auth/verify-totp`
تأكيد رمز Google Authenticator لاستكمال الدخول والحصول على JWT Token.

- **Request Body:**
```json
{
  "email": "admin@gymsaas.com",
  "code": "123456",
  "secretIfSetup": "OPTIONAL_SECRET_ON_FIRST_SETUP"
}
```

- **Response:**
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6...",
    "mustChangePassword": true
  }
}
```

---

#### `POST /api/auth/login/owner`
تسجيل دخول الأونر.

- **Request Body:**
```json
{
  "email": "owner@facility.com",
  "password": "Password123!"
}
```

- **Response:**
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1...",
    "contractSigned": false,
    "onboardingCompleted": false,
    "facilityStatus": "Active"
  }
}
```

---

#### `POST /api/auth/impersonate`
تبديل دور السوبرفايزر لتمثيل Role محدد داخل منشأة.

- **Request Body:**
```json
{
  "facilityId": 1,
  "targetRole": "Owner",
  "branchId": null
}
```

- **Response:**
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1...",
    "expiresAt": "2026-09-01T02:00:00Z"
  }
}
```

---

### 4.2 Facilities (`/api/facilities`)

#### `POST /api/facilities` (Supervisor only)
إنشاء منشأة جديدة وتخصيص الأونر والاشتراك الأساسي.

- **Request Body:**
```json
{
  "name": "جيم الأبطال",
  "description": "فرع المعادي",
  "licenseType": "Subscription",
  "licenseEndDate": "2027-09-01T00:00:00Z",
  "ownerName": "كابتن أحمد",
  "ownerEmail": "ahmed@gym.com",
  "ownerPassword": "OwnerPassword123!"
}
```

- **Response (201 Created):**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "name": "جيم الأبطال",
    "licenseType": "Subscription",
    "status": "Active",
    "createdAt": "2026-09-01T01:00:00Z"
  }
}
```

---

#### `POST /api/facilities/{id}/lock` (Supervisor only)
قفل وتجميد المنشأة فورياً بقطع أي سيشن شغالة.

- **Response:**
```json
{
  "success": true,
  "message": "تم قفل المنشأة بنجاح."
}
```

---

#### `POST /api/facilities/{id}/unlock` (Supervisor only)
فك قفل المنشأة بعد تأكيد الدفع يدويًا.

- **Headers:** `Idempotency-Key: c9d8e7f6-a5b4-3210`
- **Request Body:**
```json
{
  "amountPaid": 5000.00
}
```

- **Response:**
```json
{
  "success": true,
  "message": "تم فك قفل المنشأة وتسجيل الدفعة بنجاح."
}
```

---

### 4.3 Contracts & Signature (`/api/contracts`)

#### `GET /api/contracts/current`
عرض أحدث نسخة من العقد الإلكتروني للإطلاع والتوقيع.

- **Response:**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "version": 1,
    "content": "<h1>عقد استخدام منصة إدارة الجيمات SaaS</h1>...",
    "createdAt": "2026-01-01T00:00:00Z"
  }
}
```

---

#### `POST /api/contracts/sign`
توقيع العقد إلكترونياً باسم الأونر (المعروض بخط `Aref Ruqaa`).

- **Request Body:**
```json
{
  "contractId": 1,
  "signatureText": "أحمد محمد محمود"
}
```

- **Response:**
```json
{
  "success": true,
  "message": "تم توقيع العقد بنجاح."
}
```

---

### 4.4 Owners & Onboarding (`/api/owners`)

#### `POST /api/owners/onboarding`
إكمال بيانات التهيئة الأولى بعد توقيع العقد.

- **Request Body:**
```json
{
  "facilityPhone": "01000000000",
  "mainBranchName": "الفرع الرئيسي",
  "mainBranchAddress": "شارع النصر، المعادي"
}
```

- **Response:**
```json
{
  "success": true,
  "message": "تم إكمال التجهيز الأولي بنجاح."
}
```

---

### 4.5 Revenue & Payments (`/api/payments`)

#### `GET /api/payments/revenue-overview` (Supervisor only)
تقرير الإيرادات مفصّلاً بين الاشتراكات الأساسية والـ Add-ons.

- **Response:**
```json
{
  "success": true,
  "data": {
    "totalRevenue": 25000.00,
    "primarySubscriptionsRevenue": 20000.00,
    "addOnFeaturesRevenue": 5000.00,
    "recentPayments": [
      {
        "id": 1,
        "facilityId": 1,
        "facilityName": "جيم الأبطال",
        "amount": 5000.00,
        "paymentType": "PlatformSubscription",
        "addOnFeatureName": null,
        "recordedAt": "2026-09-01T01:00:00Z",
        "notes": "تجديد اشتراك منصة"
      }
    ]
  }
}
```

---

### 4.6 Add-on Features (`/api/addons`)

#### `POST /api/addons` (Supervisor only)
إنشاء خطة ميزة إضافية (مثل متجر أونلاين).

- **Request Body:**
```json
{
  "name": "المتجر الإلكتروني",
  "description": "بيع المكملات والمنتجات أونلاين",
  "price": 1500.00
}
```

- **Response:**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "name": "المتجر الإلكتروني",
    "description": "بيع المكملات والمنتجات أونلاين",
    "price": 1500.00,
    "isActiveForSale": true
  }
}
```

---

#### `POST /api/addons/activate` (Supervisor only)
تفعيل ميزة إضافية لمنشأة وتسجيل دفعة أوفلاين.

- **Headers:** `Idempotency-Key: a1b2c3d4-e5f6-7890`
- **Request Body:**
```json
{
  "facilityId": 1,
  "addOnFeatureId": 1,
  "amountPaid": 1500.00
}
```

- **Response:**
```json
{
  "success": true,
  "message": "تم تفعيل الميزة الإضافية وتسجيل الدفعة بنجاح."
}
```

---

### 4.7 Audit Logs (`/api/auditlog`)

#### `GET /api/auditlog?pageNumber=1&pageSize=20`
عرض سجل الأنشطة بحسب الصلاحيات والـ Role (ممنوع على Coach و Receptionist).

- **Response:**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 1,
        "actorId": "1",
        "actorType": "Supervisor",
        "onBehalfOfRole": null,
        "actionType": "create",
        "entityType": "Facility",
        "entityId": "1",
        "oldValue": null,
        "newValue": "{\"Name\":\"جيم الأبطال\"}",
        "timestamp": "2026-09-01T01:00:00Z",
        "facilityId": 1,
        "branchId": null,
        "correlationId": "a1b2c3d4"
      }
    ],
    "totalCount": 1,
    "pageNumber": 1,
    "pageSize": 20
  }
}
```
