# MEMORY.md - Fuente de Verdad del Proyecto

> **Instrucción para el Agente:** Este archivo es la **Fuente de Verdad** del proyecto. Todo plan aprobado por el usuario debe registrarse aquí antes de su implementación, y el estado de cada tarea debe actualizarse conforme avance el proyecto. No avances a la siguiente tarea sin haber completado la anterior.

---

## 1. Contexto y Arquitectura Base Aprobada
- **Propósito:** Plantilla corporativa Golden Path (`app_template_fullstack`) para desarrollo asistido por IA (Vicoders) bajo gobernanza de TI.
- **Stack Oficial:**
  - **Frontend:** React + TypeScript + Vite + Vitest sobre **Node.js 26 LTS**.
  - **Backend:** C# `.NET 10` (`net10.0`) Minimal APIs + Entity Framework Core + `xUnit` en **Clean Architecture** (`Domain`, `Application`, `Infrastructure`, `Api`, `Tests`).
  - **Base de Datos (3 Entornos Separados y Preconfigurados):**
    - **Local:** `VicoderLocalDb` en `localhost\SQLEXPRESS` (configurada una sola vez en variable de entorno de Windows `ConnectionStrings__DefaultConnection` con `Trusted_Connection=True`, cero contraseñas).
    - **QA (GCP Cloud SQL):** `BooksDb_QA` inyectada vía **GCP Secret Manager** (`db-connection-string-qa:latest`).
    - **Producción (GCP Cloud SQL):** `BooksDb_Prod` inyectada vía **GCP Secret Manager** (`db-connection-string-prod:latest`).
  - **Despliegue:** GitHub Actions hacia **Google Cloud Run** (`qa` automático y `production` protegido con aprobación obligatoria de `888Leonidas888`).

---

## 2. Reglas Operativas Activas
1. **Conventional Commits v1.0.0** en todos los commits.
2. **Cero commits directos en `main`** (trabajo exclusivo en ramas `feat/*`, `fix/*`, `docs/*`, `chore/*`, `refactor/*`).
3. **Pull Request obligatorio** vía `gh pr create`; **prohibido fusionar a `main`** sin instrucción explícita del usuario.
4. **Secuencialidad:** Terminar y verificar cada tarea antes de pasar a la siguiente.
5. **Cero archivos basura** y **cero exposición de datos sensibles**.
6. **Principio DRY** y **cero fallbacks silenciosos** (preguntar al usuario ante cualquier duda técnica).
7. **Sincronización documental:** Actualizar `README.md` y `MEMORY.md` en cada entrega.

---

## 3. Historial de Planes y Estado de Tareas

### Plan Inicial: Configuración del Template Corporativo (`app_template_fullstack`)
- [x] **Tarea 1:** Crear estructura documental del template (`README.md`, `plan.md`, `AGENTS.md`, `.gitignore`) y pipeline `.github/workflows/ci-cd.yml` con Node 26 LTS y `.NET 10`.
- [x] **Tarea 2:** Configurar reglas de ingeniería (`.agents/rules/golden-path-rules.md`, `.github/copilot-instructions.md`), inicializar `MEMORY.md` y crear las Skills operativas en `.agents/skills/`.
- [x] **Tarea 3:** Aprobar y fusionar el Pull Request #1 de reglas y skills hacia `main`.
- [x] **Tarea 4:** Separar bases de datos por entorno (`VicoderLocalDb` en local, `BooksDb_QA` con `db-connection-string-qa` para QA, `BooksDb_Prod` con `db-connection-string-prod` para Producción), instalar Skill Global `init-vicoder-project` y asignar a `888Leonidas888` como Revisor Obligatorio en `production`.
- [x] **Tarea 5:** Corregir referencia directa a la imagen Docker del backend en el job `deploy-prod` de `.github/workflows/ci-cd.yml` para evitar que GitHub Actions filtre el output por contener `secrets.GCP_PROJECT_ID`.
- [x] **Tarea 6:** Automatizar la publicación de enlaces oficiales de QA y Producción en el resumen de GitHub Actions (`$GITHUB_STEP_SUMMARY`) y en la skill `git-pr-workflow`.
- [x] **Tarea 7:** Establecer convención oficial de nombres de repositorios (`app-<area>-<nombre>`, máx. 35 caracteres) y aislamiento por esquemas SQL + Migraciones EF Core (`HasDefaultSchema` / `MigrationsHistoryTable`) para compartir la misma base de datos entre áreas.
- [x] **Tarea 8:** Exigir lógica de negocio 100% en Backend (preparado para futura unificación por microservicios) y estandarizar el Frontend con Design System Minimalista Pastel, Font Awesome Free y cero emoticones Unicode.


### Aplicación de vales de caja chica — alcance aprobado el 2026-11-17
Usuario: uso interno de Contabilidad; una sola caja chica. Aprobado en el chat: vales con responsable, concepto e importe; entrega; rendición con comprobantes y devolución; saldo e historial; reportes.

Decisiones de implementación: moneda y fondo inicial configurados por Contabilidad al abrir la caja; no precargar datos reales ni inventar saldos. Numeración correlativa; importes con dos decimales; no permitir entregas sin saldo ni cerrar rendiciones descuadradas. Comprobantes PDF/JPEG/PNG guardados en SQL con límite de 5 MB por archivo. Operaciones con control de concurrencia y trazabilidad. Una caja por instalación. Acceso local para revisión; autenticación corporativa y exposición a usuarios externos quedan pendientes de definición antes de habilitar un despliegue público.

- [x] 1. Aprovisionar repositorio desde plantilla; verificar QA y producción con revisor requerido.
- [x] 2. Aprobar y registrar alcance; crear rama feat/initial-app.
- [x] 3. Implementar y validar solución Clean Architecture, API, migración aislada contabilidad_cajachica y reglas contables. Compilación sin advertencias; 17 pruebas backend aprobadas.
- [x] 4. Implementar y validar interfaz React para configuración, vales, rendiciones, movimientos y reportes. Siete pruebas Vitest y dos flujos completos en Edge (escritorio y móvil) aprobados. Compilación TypeScript/Vite correcta; npm audit sin vulnerabilidades conocidas. Verificación visual de ambas resoluciones completada.
- [x] 5. Documentar ejecución, preparar contenedores, revisar cambios y abrir PR sin fusionar. Entrega: https://github.com/project-ia-2099/app-contabilidad-cajachica/pull/1 (rama feat/initial-app).

Validación final: 17 pruebas xUnit, 7 Vitest y 2 pruebas Edge (escritorio/móvil) aprobadas. TypeScript y build de producción correctos; npm audit: cero vulnerabilidades. Aplicación local iniciada con migración SQL y /health=ok; revisión mediante navegador real sin errores y caja sin datos de prueba. Scripts de inicio y parada, documentación y contenedores preparados. Docker no está disponible en esta máquina; no se ejecutó build de imágenes. SDK local 11.0.100 con destino y runtime .NET 10; CI y Docker usan SDK 10. Despliegue cloud deshabilitado hasta integrar acceso corporativo. Rama publicada y PR #1 abierto; sin fusión a main.

### Demo QA sin inicio de sesión — aprobado el 2026-11-17
El usuario solicita omitir autenticación por tratarse de una demostración. El despliegue hacia GCP debe ejecutarse exclusivamente desde GitHub Actions, nunca desde la computadora local. Se conserva el flujo de revisión: 888Leonidas888 revisa y fusiona el PR a main.

- [x] Confirmar que el PR #1 fue fusionado por 888Leonidas888 y que los filtros TI pasaron en main.
- [x] Revisar el bloqueo actual de QA y las dependencias del pipeline; solo se realizaron consultas de GCP, sin despliegue local.
- [x] Ajustar el workflow para publicar QA sin exigir autenticación de usuarios, incluyendo el origen CORS del frontend y disparo por cambios del workflow. El pipeline verifica API, CORS y frontend antes de presentar enlaces. Producción mantiene su condición independiente.
- [x] Validar el cambio y abrir PR para revisión y fusión por 888Leonidas888. actionlint 1.7.12 sin errores y diff verificado; PR #2: https://github.com/project-ia-2099/app-contabilidad-cajachica/pull/2, rama fix/qa-demo. Revisión solicitada a 888Leonidas888.
- [ ] Tras la fusión, seguir GitHub Actions y entregar la URL de QA cuando finalice correctamente.

La producción conserva su control independiente y aprobación requerida. Los servicios QA se usarán para la demostración; no ingresar datos contables reales.

### Demo en producción — solicitud del usuario el 2026-11-17
El usuario confirma conformidad con QA y solicita continuar con una demostración en producción, manteniendo por ahora la aplicación sin inicio de sesión. Toda publicación en GCP debe ejecutarse exclusivamente desde GitHub Actions. El entorno production conserva la aprobación de 888Leonidas888.

- [x] Verificar que el PR #2 fue fusionado y el job QA del run 44115931405 terminó correctamente.
- [x] Registrar conformidad del usuario con QA.
- [x] Preparar configuración CORS y comprobaciones de salud en producción, y ejecución manual explícita para el pase sin cambiar variables permanentes del repositorio. Casilla deploy_production desmarcada por defecto; se mantiene la aprobación requerida en production.
- [ ] Validar workflow y abrir PR para revisión y fusión por 888Leonidas888.
- [ ] Iniciar el pipeline desde GitHub sobre main aprobado; esperar aprobación del entorno production por el revisor.
- [ ] Verificar el despliegue y entregar el enlace oficial de producción.
