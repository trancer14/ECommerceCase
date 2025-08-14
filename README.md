# ECommerce Mini Backend (API + Worker)

Sipariş alan, RabbitMQ’ya event basan ve Worker ile işleyen; Redis cache ve Serilog loglama kullanan mini e-ticaret backend örneği.

## İçindekiler
- [Mimari](#mimari)
- [Önkoşullar](#önkoşullar)
- [Hızlı Başlangıç](#hızlı-başlangıç)
- [Konfigürasyon](#konfigürasyon)
- [Veritabanı (Migrations veya SQL Script)](#veritabanı-migrations-veya-sql-script)
- [Çalıştırma](#çalıştırma)
- [API Uçları](#api-uçları)
- [Gözlem / Yönetim](#gözlem--yönetim)
- [Loglama & Correlation ID](#loglama--correlation-id)
- [Testler](#testler)
- [Sorun Giderme](#sorun-giderme)

---

## Mimari

- **API (ECommerce.Api)**
  - `POST /orders` → isteği doğrular, siparişi DB’ye **Pending** kaydeder, **order-placed** event’i publish eder.
  - `GET /orders/{userId}` → ilgili kullanıcının siparişlerini döner. Sonuçlar Redis’te **2 dakika TTL** ile cache’lenir. Yeni siparişle cache **invalide** edilir.
- **Worker (ECommerce.Worker)**
  - RabbitMQ’daki **order-placed** kuyruğunu dinler.
  - Siparişi **Processed** yapar, Redis’e `order:{id}:processedAt` yazar.
  - Konsola bildirim simülasyonu loglar.
- **Katmanlar**: Domain / Application / Infrastructure / Shared (kontratlar) – Repository/UoW + MassTransit + EF Core.

Akış: **API POST** → DB’ye Pending → **RabbitMQ publish** → **Worker consume** → DB’de Processed → **Redis log** → **API GET** (cache ile).

---

## Önkoşullar

- .NET SDK **8.0+**
- Docker Desktop (Compose)
- (Opsiyonel) EF CLI:
  ```bash
  dotnet tool install -g dotnet-ef
  ```

---

## Hızlı Başlangıç

1) **Altyapıyı** Docker’da kaldır:
```bash
docker compose up -d
```
Açılan servisler:
- SQL Server → `localhost:1433` (sa / **YourStrong!Passw0rd**)
- RabbitMQ → `localhost:5672` (guest/guest), UI: `http://localhost:15672`
- Redis → `localhost:6379`

2) **Restore & Build**:
```bash
dotnet restore
dotnet build
```

3) **DB migration’larını uygula**:
```bash
dotnet ef database update -p ECommerce.Infrastructure -s ECommerce.Api
```

4) **Uygulamaları çalıştır** (iki ayrı terminal):
```bash
dotnet run --project ECommerce.Worker
dotnet run --project ECommerce.Api
```
> Swagger genellikle `http://localhost:5000/swagger` (port profilinize göre terminaldeki URL’yi kullanın).

---

## Konfigürasyon

Geliştirme ortamı (`appsettings.Development.json`) örneği:

**API**
```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost,1433;Database=ECommerceDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
  },
  "RabbitMq": { "Host": "localhost", "Port": 5672, "Username": "guest", "Password": "guest" },
  "Redis": { "Configuration": "localhost:6379" },
  "Jwt": { "Issuer":"ECommerce","Audience":"ECommerce.Clients","Key":"supersecret_dev_key_please_change","ExpiresMinutes":60 },
  "Serilog": {
    "MinimumLevel": { "Default": "Information" },
    "WriteTo": [
      { "Name":"Console" },
      { "Name":"File","Args": { "path":"logs/api-.log","rollingInterval":"Day" } }
    ],
    "Enrich": [ "FromLogContext" ]
  }
}
```

**Worker**
```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost,1433;Database=ECommerceDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
  },
  "RabbitMq": { "Host": "localhost", "Port": 5672, "Username": "guest", "Password": "guest" },
  "Redis": { "Configuration": "localhost:6379" },
  "Serilog": {
    "MinimumLevel": { "Default": "Information" },
    "WriteTo": [
      { "Name":"Console" },
      { "Name":"File","Args": { "path":"logs/worker-.log","rollingInterval":"Day" } }
    ],
    "Enrich": [ "FromLogContext" ]
  }
}
```

> Uygulamaları **Docker container içinde** çalıştıracaksan bağlantı adreslerini `localhost` yerine `mssql`, `rabbitmq`, `redis` yapın.

---

## Veritabanı (Migrations veya SQL Script)

- **Migrations ile** (önerilen):
  ```bash
  dotnet ef database update -p ECommerce.Infrastructure -s ECommerce.Api
  ```

- **SQL script ile** (opsiyonel): `db/init.sql`
  ```sql
  CREATE DATABASE ECommerceDb;
  GO
  USE master;
  IF NOT EXISTS (SELECT * FROM sys.sql_logins WHERE name = 'ECommerce')
  BEGIN
    CREATE LOGIN ECommerce WITH PASSWORD = 'EComMdl0ng!Pass';
  END
  GO
  USE ECommerceDb;
  IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'ECommerce')
  BEGIN
    CREATE USER ECommerce FOR LOGIN ECommerce;
    EXEC sp_addrolemember N'db_owner', N'ECommerce';
  END
  GO
  ```
  > Connection string:  
  `Server=localhost,1433;Database=ECommerceDb;User Id=ECommerce;Password=EComMdl0ng!Pass;TrustServerCertificate=True`

---

## Çalıştırma

**Seçenek A — Lokal (Docker’da sadece altyapılar):**
```bash
docker compose up -d
dotnet ef database update -p ECommerce.Infrastructure -s ECommerce.Api
dotnet run --project ECommerce.Worker
dotnet run --project ECommerce.Api
```

**Seçenek B — Visual Studio:**
- Solution’a sağ tık → **Set Startup Projects…** → **Multiple** → `ECommerce.Api` + `ECommerce.Worker`

---

## API Uçları

### POST /orders
```http
POST /orders
Content-Type: application/json
Authorization: Bearer <token>

{
  "userId": "u1",
  "productId": "p1",
  "quantity": 2,
  "paymentMethod": "CreditCard" // veya "BankTransfer"
}
```
**202 Accepted** döner ve gövdede `{ "id": "<guid>" }` içerir. Worker mesajı işledikten sonra kayıt **Processed** olur.

### GET /orders/{userId}
```http
GET /orders/u1
Authorization: Bearer <token>
```
Kullanıcının siparişlerini döner; sonuçlar Redis’te **2 dk TTL** ile cache’lenir.

> **Swagger** üzerinden deneyebilirsiniz: `/swagger`

---

## Gözlem / Yönetim

- **RabbitMQ UI**: `http://localhost:15672` (guest/guest)  
  - Queue: `order-placed` → Worker açıksa **Consumers ≥ 1**  
  - Mesajlar: **Ready → Unacked → Ack**
- **Redis**:
  ```bash
  redis-cli -h localhost -p 6379
  keys *processedAt
  ```
- **Loglar**:
  - API: `ECommerce.Api/logs/`
  - Worker: `ECommerce.Worker/logs/`

---

## Loglama & Correlation ID

- Serilog ile **console + file** sink aktif.
- Tüm istek/yanıtlarda **X-Correlation-ID** header’ı set edilir. Aynı ID hem API hem Worker loglarında izlenebilir.

---

## Testler

```bash
dotnet test ECommerce.Tests
```
- **Unit**: OrderService publish + cache invalidation + cache hit
- **Worker**: Consumer DB’yi Processed yapar, Redis’e `processedAt` yazar
- **Integration**: `POST /orders` (202 + id), `GET /orders/{userId}` (cache’li yanıt)

---

## Sorun Giderme

- **SQL Login failed for user 'sa'**  
  - `docker compose logs mssql` ile hazır olana kadar bekleyin.  
  - Connection string’de `TrustServerCertificate=True` olduğundan emin olun.  
  - Compose’taki **SA_PASSWORD** ile appsettings aynı olmalı.
- **RabbitMQ bağlantı hatası**  
  - `docker compose ps` → `rabbitmq` ayakta mı?  
  - API & Worker Rabbit ayarları: host/port/user/pass doğru mu?
- **Redis bağlantı hatası**  
  - `docker compose ps` → `redis` ayakta mı?  
  - `redis-cli PING` → `PONG` beklenir.
- **Swagger’da token alanı yok**  
  - SecurityDefinition/Requirement konfigürasyonunu açtığınızdan emin olun. (Projede eklendi.)
- **Worker tüketmiyor**  
  - Worker konsolunda `Endpoint Ready: order-placed` görüyor musunuz?  
  - RabbitMQ UI’da queue ve binding’leri kontrol edin.

---
