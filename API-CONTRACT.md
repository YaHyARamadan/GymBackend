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

⚠️ ملاحظة مهمة: صلاحيات الـ backend أثناء الـ impersonation بتتحدد فعليًا بـ `on_behalf_of_role`، مش بأي claim تاني — يعني لو السوبرفايزر عمل impersonate بدور Coach، هيشوف بالظبط اللي الـ Coach الحقيقي يشوفه (زي: ممنوع من `GET /api/auditlog` بالكامل)، مش صلاحيات Supervisor موسّعة. الفرونت لازم يعامل الـ impersonation session بنفس قيود الدور المختار بالظبط، مش كاستثناء.

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
    "temporaryToken": "SERVER_SIGNED_TEMP_TOKEN",
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
    "temporaryToken": "SERVER_SIGNED_TEMP_TOKEN",
    "mustChangePassword": false
  }
}
```

⚠️ **`temporaryToken` ماعادش الإيميل ولا السكرت الخام** — بقى توكن قصير العمر (5 دقايق) موقّع بالسيرفر (`TotpSetupTokenService`)، بيتولّد **بعد نجاح فحص الباسورد فقط**. الفرونت يبعته زي ما هو لـ `verify-totp`، مايقدرش يستخرج منه أو يعدّل فيه أي حاجة.

---

#### `POST /api/auth/verify-totp`
تأكيد رمز Google Authenticator لاستكمال الدخول والحصول على JWT Token.

- **Request Body:**
```json
{
  "tempToken": "SERVER_SIGNED_TEMP_TOKEN_FROM_LOGIN_STEP",
  "code": "123456"
}
```
⚠️ **مفيش `email` ولا `secretIfSetup` في الطلب** — الـ `supervisorId` والسكرت (وقت أول إعداد) بيتم استخراجهم من `tempToken` نفسه على السيرفر، مش من أي حاجة تانية بيبعتها العميل. الـ endpoint ده **لا يعمل إطلاقًا من غير `tempToken` صادر فعليًا من `login/supervisor`** بعد نجاح الباسورد.

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

#### `POST /api/auth/change-password`
تغيير كلمة السر (Supervisor أو Owner، متاح دايمًا، **إجباري** لو `mustChangePassword: true` في التوكن — أي endpoint تاني غيره وغير `logout` هيترفض بـ `403` طول ما الفلاج ده شغال).

- **Headers:** `Authorization: Bearer <Token>` (توكن عادي، حتى لو `mustChangePassword: true`)
- **Request Body:**
```json
{
  "currentPassword": "OldPassword123!",
  "newPassword": "NewStrongerPassword456!"
}
```
- **Response:**
```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1...",
    "mustChangePassword": false
  }
}
```
⚠️ التوكن القديم بيتلغى تلقائيًا بعد تغيير الباسورد (`TokenVersion` بتزيد بواحد على مستوى الحساب) — أي توكن اتصدر قبل التغيير مايشتغلش تاني، حتى لو لسه في تاريخ انتهائه.

---

#### `GET /api/auth/me`
Returns the authenticated session context used by the Frontend after login or page refresh.

- Headers: Authorization: Bearer <Token>
- Response:
{
  "success": true,
  "data": {
    "userId": "1",
    "email": "owner@facility.com",
    "actorType": "Owner",
    "facilityId": 1,
    "branchId": null,
    "isImpersonating": false,
    "onBehalfOfRole": null,
    "mustChangePassword": false
  }
}

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

- **Headers:** `Authorization: Bearer <Normal Supervisor Token>`
- **Request Body:**
```json
{
  "facilityId": 1,
  "targetRole": "Owner",
  "branchId": null
}
```
⚠️ `targetRole` مقيّد بقيم `Owner | BranchManager | Coach | Receptionist` بس — أي قيمة تانية (بما فيها `Supervisor` نفسها) بترفض.

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

---

### 4.8 Branches (`/api/branches`)

#### `POST /api/branches`
إضافة فرع جديد (`FacilityId` بياخد قيمته من التوكن، مش من الطلب).

- **Request Body:**
```json
{
  "name": "فرع سموحة",
  "address": "شارع فؤاد، سموحة، الإسكندرية",
  "phone": "0312345678"
}
```
- **Response:**
```json
{
  "success": true,
  "data": {
    "id": 3,
    "name": "فرع سموحة",
    "address": "شارع فؤاد، سموحة، الإسكندرية",
    "phone": "0312345678",
    "facilityId": 1
  }
}
```

---

### 4.9 Players (`/api/players`)

#### `POST /api/players`
إضافة لاعب جديد (`FacilityId` من التوكن؛ `branchId` يتم التحقق منه إنه تابع لنفس المنشأة قبل الإضافة — أي `branchId` تابع لمنشأة تانية يترفض بـ `404 NotFound`).

- **Request Body:**
```json
{
  "name": "أحمد محمد",
  "email": "ahmed@example.com",
  "phone": "01012345678",
  "dateOfBirth": "1995-03-10",
  "branchId": 3
}
```
- **Response:**
```json
{
  "success": true,
  "data": {
    "id": 42,
    "name": "أحمد محمد",
    "email": "ahmed@example.com",
    "branchId": 3,
    "facilityId": 1
  }
}
```

---

### 4.10 Support Tickets (`/api/support`)

#### `POST /api/support/tickets`
فتح تذكرة دعم داخل الداشبورد (بديل التواصل الخارجي — الأونر يتواصل مع السوبرفايزر من هنا).

- **Request Body:**
```json
{
  "subject": "مشكلة في عرض التقارير",
  "initialMessage": "التقرير الشهري مش بيظهر بيانات فرع سموحة"
}
```
- **Response:**
```json
{
  "success": true,
  "data": {
    "id": 7,
    "subject": "مشكلة في عرض التقارير",
    "status": "Open",
    "senderActorType": "Owner",
    "createdAt": "2026-09-01T10:00:00Z"
  }
}
```
⚠️ لو اتفتحت أثناء سيشن impersonation، `senderActorType` بيسجل **الدور الحقيقي المُمثَّل** (Owner/BranchManager/إلخ)، مش `Supervisor` دايمًا.

---

## 5. Frontend Read Endpoints

These endpoints are available in addition to the original command endpoints.

| Method | Route | Access | Purpose |
|--------|-------|--------|---------|
| GET | /api/facilities | Supervisor | List all facilities |
| GET | /api/facilities/{id} | Supervisor | Read one facility |
| GET | /api/facilities/{id}/branches | Supervisor | Read branches for a facility |
| GET | /api/facilities/{id}/subscription | Supervisor | Read platform subscription |
| GET | /api/branches | Facility session | Read branches in the current tenant |
| GET | /api/players | Facility session | Read players in the current tenant |
| GET | /api/owners/me | Owner | Read owner and facility onboarding state |
| GET | /api/addons | Supervisor | Read add-on pricing plans |
| GET | /api/addons/facility/{facilityId} | Supervisor | Read activated add-ons for a facility |
| GET | /api/support/tickets | Owner or Supervisor | Read support tickets and messages |

All responses use the standard { "success": true, "data": ... } envelope. Facility-scoped reads derive the facility from the token; supervisor-only facility routes take the id as a route parameter.

    
## 6. Supervisor Management Endpoints

All endpoints below require a normal Supervisor JWT unless explicitly stated otherwise.

| Method | Route | Purpose |
|---|---|---|
| POST | /api/auth/login/staff | Login for BranchManager, Coach, or Receptionist |
| POST | /api/auth/logout | Revoke the current JWT server-side |
| GET | /api/dashboard/supervisor-overview | Platform counts, revenue, tickets, and unread notifications |
| PUT | /api/facilities/{id} | Update facility details and license |
| DELETE | /api/facilities/{id} | Permanently delete facility tenant data while preserving audit history |
| GET | /api/owners | List owners, optionally filtered by facilityId |
| PATCH | /api/owners/{id} | Update owner profile and access flags |
| POST | /api/owners/{id}/reset-password | Reset an owner password |
| GET | /api/employees | List staff, optionally filtered by facilityId |
| POST | /api/employees | Create BranchManager, Coach, or Receptionist |
| PATCH | /api/employees/{role}/{id}/status | Activate or deactivate staff |
| POST | /api/employees/{role}/{id}/reset-password | Reset staff password |
| GET | /api/facilities/{id}/players | List all players in a facility |
| POST | /api/facilities/{id}/players | Create a player in a facility |
| PUT | /api/facilities/{id}/players/{playerId} | Update player data or active state |
| GET | /api/facilities/{id}/subscriptions | List player subscriptions in a facility |
| POST | /api/facilities/{id}/players/{playerId}/subscription | Assign a subscription to a player |
| GET | /api/payments/records | Filtered and paginated payment records |
| GET | /api/payments/report | Grouped payment report by facility and payment type |
| POST | /api/support/tickets/{id}/messages | Reply to a support ticket |
| POST | /api/support/tickets/{id}/close | Close a support ticket |
| GET | /api/notifications | Read notifications for the current actor |
| POST | /api/notifications/{id}/read | Mark one notification as read |
| POST | /api/notifications/read-all | Mark all notifications as read |

Deleting a facility removes its tenant records, staff, players, subscriptions, payments, tickets, and add-on subscriptions. Audit log rows are intentionally retained.
