# Caja chica — Contabilidad

Aplicación interna para una caja chica: apertura, vales correlativos, entrega de dinero, rendición con comprobantes, devolución, reposición, saldo, historial y reportes por período en CSV.

## Uso

1. Abra la caja con una moneda (PEN, USD o EUR) y el fondo inicial real.
2. Registre un vale con fecha, responsable, concepto e importe. Su número se asigna automáticamente; este registro todavía no descuenta saldo.
3. En **Ver detalle → Registrar entrega**, confirme el dinero entregado. La API verifica el saldo y descuenta el importe.
4. Agregue comprobantes PDF, PNG o JPEG, hasta 5 MB por archivo, indicando fecha, número e importe del gasto.
5. Cierre la rendición indicando el dinero efectivamente devuelto. Los comprobantes más la devolución deben completar el importe del vale. El cierre incorpora la devolución al saldo y bloquea el vale.
6. Use **Reponer fondo** para registrar ingresos adicionales con su referencia. **Movimientos** conserva el historial. **Reportes** filtra por fecha del vale y exporta su estado actual.

No se precargan datos de demostración en la base contable. Los vales entregados sin gastos pueden cerrarse devolviendo el importe completo. No se permiten gastos mayores al importe del vale ni modificar vales cerrados. Esta primera versión no incluye anulación de vales, correcciones retroactivas ni varias cajas.

## Ejecución local en Windows

Requisitos: SDK compatible con .NET 10, runtime ASP.NET Core 10, Node.js 26, SQL Server local configurado por TI y variable de entorno `ConnectionStrings__DefaultConnection`. Nunca guarde esa variable ni credenciales en archivos versionados.

Desde la carpeta del proyecto:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Start-Local.ps1
```

El iniciador compila, aplica las migraciones, comprueba SQL e inicia procesos sin ventanas de terminal. Interfaz: http://127.0.0.1:5173. API: http://127.0.0.1:5100. Solo se enlazan a la interfaz local de esta computadora. Registros e identificadores de procesos se guardan en `work/`, excluido de Git.

Para detener las instancias creadas por el iniciador:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Stop-Local.ps1
```

Para desarrollo manual, inicie cada componente en una terminal:

```powershell
$env:AllowedOrigins__0 = 'http://127.0.0.1:5173'
dotnet run --project backend/Cash.Api --urls http://127.0.0.1:5100
```

```powershell
$env:VITE_API_URL = 'http://127.0.0.1:5100'
npm ci --prefix frontend
npm run dev --prefix frontend
```

## Pruebas

```powershell
dotnet test backend/Cash.sln --configuration Release
npm test --prefix frontend
npm run build --prefix frontend
npm audit --prefix frontend
```

Las 17 pruebas backend verifican arquitectura, numeración, saldo, estados, archivos, conciliación, historial, concurrencia, reportes y neutralización de fórmulas en CSV. Usan integración HTTP con EF InMemory; la migración y conexión SQL se comprueban por separado al iniciar la aplicación local.

Las siete pruebas de interfaz verifican apertura, registro, errores, adjuntos, reportes y fallos de actualización después de guardar. Dos pruebas de navegador completan el ciclo en escritorio y móvil, verifican el PDF multipart y la descarga CSV. Usan una API controlada de prueba y no escriben datos en la caja real.

Para ver las pruebas en una ventana de Edge:

```powershell
$env:PLAYWRIGHT_CHANNEL = 'msedge'
$env:PLAYWRIGHT_SLOW_MO = '400'
npm run test:browser:visible --prefix frontend
```

Para segundo plano, use `npm run test:browser --prefix frontend`. Sin `PLAYWRIGHT_CHANNEL`, instale Chromium con `npx --prefix frontend playwright install chromium`. Las pruebas usan el puerto 5175; resultados y capturas se excluyen de Git.

## Arquitectura y persistencia

- `backend/Cash.Domain`: entidades y contrato de repositorio, sin dependencias de persistencia.
- `backend/Cash.Application`: reglas contables, DTOs y servicios; solo depende de Domain.
- `backend/Cash.Infrastructure`: EF Core 10, SQL Server, repositorios y migraciones.
- `backend/Cash.Api`: Minimal APIs, errores explícitos, CORS por lista de orígenes y `/health` con comprobación de base de datos.
- `backend/Cash.Tests`: xUnit, integración HTTP y arquitectura.
- `frontend`: React, TypeScript, Vite, Vitest, Playwright, Inter local y Font Awesome Free. Los cálculos y reglas contables están en el backend.

Todas las tablas y el historial EF usan el esquema `contabilidad_cajachica`. Las mutaciones modifican una revisión de la caja para detectar concurrencia y se guardan mediante una única llamada transaccional de EF en SQL Server. Las respuestas 409 requieren actualizar la vista. Comprobantes y metadatos se conservan en SQL; no se guardan datos contables en el navegador. El historial registra operaciones y fechas; la identificación individual de operadores depende de la futura autenticación.

Para generar migraciones:

```powershell
dotnet tool restore
dotnet ef migrations add NombreDelCambio --project backend/Cash.Infrastructure --startup-project backend/Cash.Api
```

## API

| Método | Ruta | Función |
| --- | --- | --- |
| GET | `/health` | Verifica acceso a la base de datos |
| GET | `/api/summary` | Saldo, moneda e indicadores |
| POST | `/api/fund` | Abre la única caja |
| POST | `/api/fund/top-ups` | Registra reposición |
| GET, POST | `/api/vouchers` | Lista o registra vales |
| GET | `/api/vouchers/{id}` | Detalle y comprobantes |
| POST | `/api/vouchers/{id}/deliver` | Registra entrega |
| POST | `/api/vouchers/{id}/receipts` | Multipart: date, reference, amount, file |
| POST | `/api/vouchers/{id}/close` | Cierra con devolución |
| GET | `/api/receipts/{id}` | Descarga comprobante |
| GET | `/api/movements` | Historial |
| GET | `/api/reports?from=AAAA-MM-DD&to=AAAA-MM-DD` | Reporte por fecha del vale |
| GET | `/api/reports/export?from=AAAA-MM-DD&to=AAAA-MM-DD` | CSV |

## Contenedores y revisión

Los Dockerfiles usan .NET 10 y Node 26, con nginx en el frontend. `docker compose up --build` requiere `SQL_PASSWORD` y `CASH_CONNECTION_STRING` desde el entorno, fuera del repositorio; la conexión debe apuntar al servicio `sqlserver`. El volumen `cash-sql` conserva datos. Los puertos publicados se limitan a 127.0.0.1.

El repositorio tiene entornos QA y producción; producción requiere revisión de TI. El workflow comprueba backend e interfaz en cada PR. El despliegue cloud queda deshabilitado por defecto: esta versión no incluye autenticación corporativa. Antes de habilitar `ENABLE_CLOUD_DEPLOYMENT=true`, TI debe integrar el acceso autorizado y configurar `AllowedOrigins` con la URL del frontend. No publique esta versión con acceso público a los datos contables.

Los cambios se entregan en una rama y un Pull Request, sin fusionarse automáticamente a `main`.
