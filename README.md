# AMBC Basketball

Aplicación web mobile-first para la Asociación Municipal de Baloncesto Cochabamba.

- **Backend:** ASP.NET Core 10 + EF Core 10 + PostgreSQL 18 (carpeta `backend/Ambc.Api`)
- **Frontend:** React 19 + Vite + TypeScript (carpeta `frontend`)
- **Tests:** xUnit + SQLite in-memory (carpeta `tests/Ambc.Api.Tests`)

## Requisitos

- .NET SDK 10
- Node.js 22+
- PostgreSQL 18 en `localhost:5432`, base `ambc`
- Configurar la cadena de conexión y `Jwt:Key` en `backend/Ambc.Api/appsettings.json`

## Ejecutar

```bash
# backend (http://localhost:5083)
cd backend/Ambc.Api && dotnet run

# frontend (http://localhost:5173, proxya /api y /uploads al backend)
cd frontend && npm run dev
```

## Build y tests

```bash
dotnet build backend/Ambc.Api
dotnet test tests/Ambc.Api.Tests
cd frontend && npm run build
```

> Detener el backend antes de compilar: en Windows el proceso bloquea
> `bin/Debug/net10.0/Ambc.Api.exe` y el build falla con MSB3027/MSB3021.

## Estructura

```
backend/Ambc.Api/
  Controllers/     endpoints (admin, entrenador, árbitro, público)
  Data/            DbContext, migraciones EF, seeder
  Models/          entidades y DTOs
  Services/        fixture, standings, playoffs, planilla (Groq), notificaciones
  Migrations/      historial EF Core
  Resources/       planilla_guia.jpg (referencia para Groq)
frontend/src/
  components/      layout, guardas y librería de UI
  pages/           vistas por rol
  lib/             cliente axios y tipos
tests/Ambc.Api.Tests/
  StandingsTests, PlayoffTests, FixtureSemanaStatsTests
```

## Decisiones de producto

- **Estadísticas:** la planilla AMBC registra únicamente **puntos**. Las columnas
  `Reb`, `Ast`, `Stl`, `Blk`, `To` de `player_game_stats` se escriben siempre en **0**.
  El PTS se toma del dato declarado por el jugador en la planilla (o se calcula como
  `2P×2 + 3P×3 + FT` si vienen los componentes).
- **Concurrencia:** `Games.RowVersion` y `Planillas.RowVersion` son columnas `bytea`
  mantenidas por un trigger `BEFORE UPDATE` + `DEFAULT` (PostgreSQL no tiene rowversion
  nativo). Un conflicto de escritura devuelve **409 Conflict**.
- **Notificaciones:** el proceso que detecta "Hoy hay partido" corre en un
  `BackgroundService` (al arrancar y cada hora) y es idempotente vía
  `NotificationEvents` con UNIQUE `(GameId, Tipo, Fecha, RefereeId)`.
  **Supuesto:** una sola instancia del backend (no hay lock distribuido). Si se
  escala horizontalmente, hace falta coordinación entre instancias.
- **Ida y vuelta:** el generador round-robin produce la vuelta con la localía
  invertida respecto de la ida.

## Deuda técnica aceptada

| Ítem | Riesgo | Estado |
|------|--------|--------|
| **CSS Modules sin scope** (`vite.config.ts: css.modules.generateScopedName = name`) | **Footgun real.** Todas las clases CSS son globales, así que dos componentes pueden definir la misma clase y una pisa a la otra **sin ningún aviso**. Ya ocurrió: `AdminDashboard.module.css` define `.stats-grid` y el editor de planillas usaba el mismo nombre → la tabla se renderizó como 2 columnas de 198px en lugar de la grilla de 7 columnas. Se detectó solo mirando la pantalla en un navegador real. | **Aceptado** (no se corrige: activar el scope obligaría a reescribir todos los componentes). **Convención obligatoria: prefijar las clases por pantalla** (`arb-`, `plan-stats-`, `adm-`…) y verificar con `grep` que el nombre no exista antes de definirlo. |
| `Game.Hora` como `string` | Sin validación de formato a nivel de tipo. | Aceptado |
| `Game.Fase` como `string` | Ídem; un enum con `HasConversion` sería más seguro. | Aceptado |
| `Notification.Destinatarios` como DSL de texto | Formato `TEAM:<id>` / `USER:<id>` parseado con strings. | Aceptado |
| Sin OpenAPI/Swagger | El contrato vive en `lib/types.ts` + DTOs. | Pendiente |
| Sin health checks ni Serilog | No hay `/health`. | Pendiente |
| PBKDF2 100k iteraciones | OWASP 2024 recomienda 600k. | Aceptado (riesgo bajo) |
| BackgroundService sin lock distribuido | Solo falla con dos instancias del backend a la vez (hoy es una). | Aceptado |

## Dependencia de SQLite en tests (`NU1903` — resuelto)

`Microsoft.EntityFrameworkCore.Sqlite` 10.0.0 arrastraba de forma transitiva
`SQLitePCLRaw.lib.e_sqlite3` **2.1.11**, que empaqueta SQLite < 3.50.2 y está afectado por
**CVE-2025-6965 / GHSA-2m69-gcr7-jv3q** (severidad alta: corrupción de memoria cuando el
número de términos agregados supera el de columnas disponibles). En su momento el advisory
no publicó versión parcheada, así que quedó como deuda aceptada.

**Resuelto** fijando explícitamente el bundle en `tests/Ambc.Api.Tests.csproj`:

```xml
<PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="3.0.5" />
```

Con eso el árbol de dependencias pasa a **`SQLite 3.53.4`** (≥ 3.50.2, parcheado) con
`SQLitePCLRaw.core` / `.provider.e_sqlite3` / `.config.e_sqlite3` en 3.0.5, sin `NU1903`
ni conflictos de versión (los tests siguen ejecutándose contra el binario nativo nuevo).

Verificado: `dotnet build` → 0 warnings / 0 errors y `dotnet test` → **20/20**.

> Si VS Code sigue mostrando la alerta tras el cambio, es caché del DesignTimeBuild:
> `Ctrl+Shift+P` → **Developer: Reload Window** (o borrar `tests/Ambc.Api.Tests/obj/` y
> rehacer el restore).

Nota: esta dependencia es **solo del proyecto de tests**. El backend usa `Npgsql`
(PostgreSQL) y no referencia SQLite.
