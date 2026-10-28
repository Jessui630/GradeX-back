# GradeX-back

Backend de **GradeX**, una plataforma para la verificación de originalidad de productos. Expone una API REST construida con ASP.NET Core y .NET 10, organizada en capas, con persistencia mediante Entity Framework Core 10, SQL Server en desarrollo y Azure SQL en el entorno de producción previsto.

El frontend React/Vite PWA se mantiene en un repositorio separado.

## Características

- API ASP.NET Core con endpoints de salud y estructura preparada para los módulos de negocio.
- Arquitectura en capas: dominio, aplicación, infraestructura y API.
- Persistencia con Entity Framework Core 10 y SQL Server.
- Modelo de datos para usuarios, productos, solicitudes de evaluación, especialistas, evaluaciones, certificados y notificaciones.
- Migraciones versionadas de Entity Framework Core.
- Pruebas automatizadas con xUnit.
- Análisis estático con los analizadores Roslyn del SDK de .NET.
- Script de calidad reproducible con logs, SARIF y resultados TRX.
- Integración continua mediante GitHub Actions.

## Tecnologías

| Componente | Versión / herramienta |
|---|---|
| SDK | .NET 10.0.402 (`global.json`) |
| API | ASP.NET Core |
| Persistencia | Entity Framework Core 10.0.13 |
| Base de datos | SQL Server LocalDB en desarrollo; Azure SQL Database en producción (condicionado a la capa gratuita) |
| Pruebas | xUnit y Microsoft.NET.Test.Sdk |
| Análisis estático | Roslyn Analyzers, `AnalysisLevel 10.0` |
| Automatización | GitHub Actions |
| Herramientas EF | `dotnet-ef` 10.0.13 |

## Arquitectura

```text
src/
  GradeX.Domain/          Entidades y contratos del dominio.
  GradeX.Application/     Casos de uso y coordinación de la aplicación.
  GradeX.Infrastructure/  Persistencia EF Core, modelos y migraciones.
  GradeX.Api/             Host ASP.NET Core, controladores y configuración HTTP.
tests/
  GradeX.Tests/           Pruebas automatizadas.
```

Dependencias principales:

- `GradeX.Domain` no depende de ASP.NET Core ni de Entity Framework Core.
- `GradeX.Application` depende de `GradeX.Domain`.
- `GradeX.Infrastructure` implementa la persistencia y depende de `GradeX.Application`.
- `GradeX.Api` compone la aplicación y expone los endpoints HTTP.

## Requisitos

- **Visual Studio 2026** con la carga de trabajo *ASP.NET and web development*, o el SDK equivalente por línea de comandos.
- **.NET SDK 10.0.402**. `global.json` selecciona esa versión con `rollForward: latestPatch`.
- **SQL Server LocalDB** para aplicar migraciones y ejecutar la API con base de datos.
- **PowerShell 7** para el script de calidad. En Windows también puede ejecutarse con PowerShell 5.1; en Linux se usa `pwsh`.
- **Git** para el control de versiones.

## Instalación

```powershell
git clone https://github.com/Jessui630/GradeX-back.git
cd GradeX-back
dotnet restore GradeX.slnx
dotnet tool restore
```

Abrir `GradeX.slnx` en Visual Studio 2026 o compilar desde la línea de comandos:

```powershell
dotnet build GradeX.slnx
```

## Configuración

La cadena de conexión de desarrollo se configura fuera del repositorio con user-secrets. La clave es `ConnectionStrings:GradeX`.

```powershell
dotnet user-secrets set "ConnectionStrings:GradeX" `
  "Server=(localdb)\MSSQLLocalDB;Database=GradeX_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" `
  --project src/GradeX.Api
```

Verificar siempre el servidor y el nombre de la base antes de aplicar cambios. No guardar cadenas de conexión con credenciales en `appsettings*.json`, en el repositorio ni en la documentación.

La API no ejecuta `Migrate()` al arrancar. Las migraciones se aplican de forma explícita.

`DesignTimeDbContextFactory` permite que las herramientas de EF Core generen migraciones y scripts sin arrancar la API ni depender de user-secrets. Usa la variable `GRADEX_DESIGN_TIME_CONNECTION` si está definida; si no, una base LocalDB `GradeX_DesignTime`. No se usa en runtime. Para generar un script idempotente, sin abrir una conexión ni aplicar cambios:

```powershell
./scripts/ef-migrations.ps1
```

El script se escribe por defecto en `artifacts/migrations/GradeX_DevCloud.sql`, muestra su SHA-256 y no reemplaza la aplicación de migraciones en desarrollo. El script generado se revisa y se ejecuta de forma manual en el entorno autorizado.

## Base de datos y migraciones

El proyecto utiliza **EF Core Migrations**. El manifiesto de herramientas se encuentra en `.config/dotnet-tools.json` y se restaura con `dotnet tool restore`.

Comandos habituales:

```powershell
# Listar migraciones
dotnet ef migrations list `
  --project src/GradeX.Infrastructure `
  --startup-project src/GradeX.Api `
  --context GradeXDbContext

# Comprobar si el modelo difiere del snapshot
dotnet ef migrations has-pending-model-changes `
  --project src/GradeX.Infrastructure `
  --startup-project src/GradeX.Api `
  --context GradeXDbContext

# Aplicar solo después de verificar el destino de GRADEX_DESIGN_TIME_CONNECTION
dotnet ef database update `
  --project src/GradeX.Infrastructure `
  --startup-project src/GradeX.Api `
  --context GradeXDbContext
```

La fábrica de diseño no usa los user-secrets de la API: `dotnet ef database update` utiliza `GRADEX_DESIGN_TIME_CONNECTION` o, si no está definida, `GradeX_DesignTime` en LocalDB. Verificar servidor y base de esa variable antes de ejecutar cualquier actualización; no aplicar migraciones en Azure desde este procedimiento.

Migraciones incluidas:

1. `20261019003256_InitialSchema`: esquema inicial.
2. `20261019010911_RestoreCheckConstraints`: restauración de restricciones CHECK.
3. `20261022203833_AddCertificateSpecialtyReference`: columna nullable `Certificado.IdEspecialidad`, índice `IX_Certificado_IdEspecialidad` y clave foránea `FK_Certificado_Especialidad` con `NO ACTION`.

Los scripts de referencia del esquema histórico están en `docs/database/InitialSchema.sql` y `docs/database/RestoreCheckConstraints.sql`.

No modificar las migraciones históricas ni regenerar el scaffold de forma destructiva. Antes de revertir una migración, verificar el entorno, el respaldo y la ausencia de datos en riesgo.

## Ejecución

```powershell
dotnet run --project src/GradeX.Api
```

La API queda disponible en la URL configurada en `src/GradeX.Api/Properties/launchSettings.json`. El endpoint de salud permite comprobar que el host responde:

```text
GET /api/health
```

El endpoint es de liveness: responde aunque Azure SQL esté en pausa y no debe usarse como prueba de conectividad de base de datos. Las pruebas unitarias no abren SQL Server. La prueba de integración HTTP usa `WebApplicationFactory` y `TestServer`, y tampoco abre SQL Server.

## Pruebas

```powershell
dotnet test GradeX.slnx
```

Pruebas incluidas actualmente:

- Contrato del controlador de salud.
- Contrato de la respuesta de salud y su versión.
- Ruta HTTP `/api/health` mediante `WebApplicationFactory` y `TestServer`, sin SQL Server.

La solución contiene cuatro pruebas automatizadas.

## Calidad de código

El análisis estático usa los analizadores Roslyn incluidos en el SDK, con `AnalysisLevel 10.0` y `AnalysisMode Recommended`.

```powershell
# Medición normal; el directorio de salida debe ser nuevo
./scripts/quality.ps1 -OutputDirectory "$env:TEMP/gradex-quality-medicion"

# Gate estricto
./scripts/quality.ps1 -OutputDirectory "$env:TEMP/gradex-quality-gate" -FailOnWarnings
```

El script ejecuta `dotnet --info`, restore, Rebuild en Release, análisis con SARIF y pruebas sin reconstruir. Genera logs, SARIF por proyecto, TRX y `summary.json`, y no sobrescribe un directorio existente.

El gate estricto utiliza dos excepciones limitadas y documentadas en `.editorconfig` y en el código:

- CA1861 únicamente en la migración histórica `InitialSchema`.
- CA1822 únicamente en `OnModelCreatingPartial`.

No se desactiva el análisis global ni se excluyen carpetas completas.

## Integración continua

`.github/workflows/quality.yml` se ejecuta en `push`, `pull_request` y `workflow_dispatch`. Usa el SDK seleccionado por `global.json`, no necesita LocalDB ni secretos personalizados, aplica el gate estricto y publica logs, SARIF y TRX como artefactos. También expone `workflow_call` para que otros flujos reutilicen el mismo gate sin duplicar comprobaciones.

`.github/workflows/deploy-dotnet.yml` es un flujo manual (`workflow_dispatch`) que reutiliza la calidad, publica `GradeX.Api` en Release, genera `gradex-api.zip`, su SHA-256 y un manifiesto, y solo despliega tras una confirmación explícita en la rama `release`. No crea recursos de Azure ni cambia su configuración: únicamente valida el destino, publica el ZIP en `GradeX-api` y lo reinicia.

`.github/workflows/rollback-dotnet.yml` es un flujo manual que valida el run de origen, recupera el artefacto `gradex-api` con su checksum y manifiesto, repite el preflight y lo vuelve a desplegar. La reversión no se ejecuta automáticamente.

`.github/workflows/verify-dotnet.yml` es un flujo manual **solo lectura** que ejecuta el preflight sin desplegar. Se lanza sobre `release` con la confirmación `VERIFY`; usa OIDC y el environment `production`, pero no reinicia ni modifica Azure. Sirve para comprobar runtime, SKU, modelo Code, identidad, settings, HTTPS y firewall antes del primer despliegue. No imprime la cadena de conexión ni IDs de suscripción/tenant.

`.github/workflows/deploy.yml` conserva el flujo heredado de construcción y publicación de la imagen del backend. No se modifica desde el flujo de calidad; no despliega en Azure y no se usa para `GradeX-api`.

## Despliegue manual en Azure App Service

El despliegue previsto usa la app Linux **`GradeX-api`** en el plan F1 existente **`ASP-gradexrg-8bed`** (grupo `gradex-rg`, región `West US`). La app anterior **`GradeX`** (Go/Site Containers) fue eliminada por el propietario: no existe un recurso de respaldo ni una reversión hacia Go, y `GradeX-api` no reutiliza su identidad administrada ni su usuario SQL. El flujo no crea recursos, no usa Docker, no usa slots y no realiza swap. La primera ejecución requiere que la suscripción, la región, el runtime, la identidad, la cadena SQL y el firewall hayan sido verificados y autorizados.

### Destino y preflight

| Elemento | Valor |
|---|---|
| Grupo de recursos | `gradex-rg` |
| Plan | `ASP-gradexrg-8bed` (F1/Free, Linux) |
| App nueva | `GradeX-api` (Linux code-only, `DOTNETCORE\|10.0`) |
| App anterior | `GradeX` (Go/Site Containers, eliminada; sin rollback) |
| Azure SQL | `sql-gradex-students.database.windows.net` / `GradeX_DevCloud` |

El workflow se niega a desplegar si el plan no es F1/Free, si la app no es Linux code-only, si `linuxFxVersion` no es `DOTNETCORE|10.0`, si la app no está en `westus`, si no tiene una identidad system-assigned exclusiva, si falta `WEBSITE_RUN_FROM_PACKAGE=1` o si la cadena de conexión no usa managed identity. También exige que todas las IP de salida posibles de `GradeX-api` estén cubiertas por reglas específicas del firewall SQL. Una regla amplia `0.0.0.0` no se acepta como cobertura de la nueva app y se reporta como advertencia; no se elimina en esta fase.

### App Settings

| App Setting | Valor previsto |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` |
| `WEBSITE_RUN_FROM_PACKAGE` | `1` |
| `SCM_DO_BUILD_DURING_DEPLOYMENT` | `false` |
| `ConnectionStrings__GradeX` | Cadena con `Authentication=Active Directory Managed Identity`, `Encrypt=True`, `TrustServerCertificate=False`, `Pooling=False` y `Connect Timeout=60`. Sin `User ID` ni contraseña. |

Plantilla de la cadena, sin secretos:

```text
Server=tcp:sql-gradex-students.database.windows.net,1433;Database=GradeX_DevCloud;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Pooling=False;Connect Timeout=60;
```

`Pooling=False` evita que conexiones ociosas mantengan Azure SQL despierto y consuman la cuota gratuita. El health check es de liveness y no consulta la base de datos.

### Identidad administrada y usuario SQL

`GradeX-api` debe tener una identidad **system-assigned** propia y exclusiva. La app `GradeX` fue eliminada, por lo que no existe una identidad ni un usuario SQL reutilizables de ese recurso. No se reutiliza ningún principal de la app eliminada. Con un administrador Entra ID de Azure SQL, en la base `GradeX_DevCloud`:

```sql
CREATE USER [GradeX-api] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [GradeX-api];
ALTER ROLE db_datawriter ADD MEMBER [GradeX-api];
```

El usuario `[GradeX]` de la app eliminada no se reutiliza. Si quedó un principal huérfano en la base, no se usa y su limpieza requiere análisis y autorización separados. La identidad de runtime solo recibe DML; las migraciones se ejecutan aparte con una identidad administrativa.

El firewall SQL debe permitir las IP de salida posibles de `GradeX-api` mediante reglas específicas. Para consultarlas:

```bash
az webapp show \
  --resource-group gradex-rg \
  --name GradeX-api \
  --query possibleOutboundIpAddresses -o tsv
```

Las IP pueden cambiar si la app se mueve o se recrea; hay que revalidarlas antes de un cambio de plan o región. No se usa una regla `0.0.0.0`/Allow Azure services para la nueva app salvo una excepción de red aprobada y documentada. Si quedó una regla amplia de la app eliminada, no se retira sin analizar su impacto; el preflight exige reglas específicas para `GradeX-api` y solo la reporta como advertencia.

### OIDC, environment y permisos

La autenticación con Azure usa OIDC y variables de repositorio, sin secretos de larga duración:

| Nombre | Tipo | Uso |
|---|---|---|
| `AZURE_CLIENT_ID` | Variable | App registration / federated credential. |
| `AZURE_TENANT_ID` | Variable | Tenant de Entra ID. |
| `AZURE_SUBSCRIPTION_ID` | Variable | Suscripción del plan y la base. |
| `production` | Environment | Política de rama `release`; configurar revisores obligatorios si están disponibles. |

La credencial federada debe ser exactamente `repo:Jessui630/GradeX-back:environment:production`, con emisor `https://token.actions.githubusercontent.com` y audiencia `api://AzureADTokenExchange`. El rol mínimo previsto es `Website Contributor` sobre `GradeX-api` y `Reader` sobre `gradex-rg` (plan, app y firewall SQL); no se asigna `Contributor` a toda la suscripción. Verificar si el environment `production` exige revisión antes de ejecutar el job de despliegue.

No confundir la identidad de ejecución de GitHub Actions (app registration con federated credential y rol `Website Contributor`) con la identidad administrada de `GradeX-api` (system-assigned, usada solo por el runtime para autenticarse en Azure SQL). Son identidades distintas y no se comparten permisos.

### Artifact, despliegue y rollback

El job `package` publica `GradeX.Api` en Release, elimina `*.pdb`, `web.config` y `appsettings.Development.json`, crea `gradex-api.zip`, calcula su SHA-256 y genera `gradex-api.manifest.json` con app, plan, grupo, runtime, commit, run, intento, destino, tamaño, número de entradas y hash. El artifact `gradex-api` se conserva 90 días.

El job `deploy` descarga el artifact, vuelve a verificar checksum, manifiesto, ZIP, runtime y contenido prohibido, ejecuta el preflight F1 y solo entonces publica:

```bash
az webapp deploy \
  --resource-group gradex-rg \
  --name GradeX-api \
  --src-path artifact/gradex-api.zip \
  --type zip \
  --clean false \
  --restart true
```

`WEBSITE_RUN_FROM_PACKAGE=1` exige `--clean false`; usar `--clean true` sería incoherente con el paquete montado. Después del despliegue se comprueba `https://<host>/api/health` sin seguir redirecciones y se valida el contrato `status=ok`, `application=GradeX API`, `version=0.1.0`. Si el smoke falla, no hay rollback automático ni existe un recurso Go al que volver.

La reversión se ejecuta manualmente con `Rollback GradeX.Api (manual)` sobre `release`, el `run_id` de un despliegue **.NET anterior exitoso** y la confirmación `ROLLBACK`. El primer despliegue no tiene una versión .NET anterior: si falla, se corrige y se reintenta; no se vuelve a Go ni a un recurso eliminado. El ZIP inicial validado se conserva como baseline offline, pero no tiene manifiesto y no es aceptado por el workflow de rollback. Tras el primer despliegue exitoso, el artifact del run (con manifiesto) y los siguientes pueden usarse como origen de rollback. El workflow valida que el run de origen sea `workflow_dispatch`, de la rama `release`, del workflow de despliegue, con conclusión `success` y el mismo intento del artifact; descarga el artifact, verifica SHA-256, manifiesto, commit, destino, runtime y contenido, repite el preflight y despliega con `--clean false`. Solo se aceptan artifacts con manifiesto; si el artifact expiró o no coincide, se detiene y se elige otro run exitoso o se reconstruye el paquete con revisión, sin saltarse el preflight.

La capa gratuita de App Service F1 tiene 1 GB de memoria, 1 GB de almacenamiento, 60 minutos de CPU al día, 165 MB de salida al día, una instancia compartida y no incluye Always On, slots, autoescalado, dominio personalizado ni certificado de dominio personalizado. La aplicación puede experimentar arranques en frío y debe usar el hostname HTTPS `*.azurewebsites.net`. F1 no tiene SLA; el despliegue es para demo/validación, no para producción con garantías de disponibilidad.

Azure SQL Free, cuando la suscripción sea elegible, ofrece 100.000 vCore-segundos al mes, 32 GB de datos y 32 GB de backup, con auto-pausa. La base debe configurarse con `AutoPause` como comportamiento al agotar la cuota; `BillOverUsage` no se permite. Si la suscripción no es elegible, no se sustituye por una base de pago.

### Orden del primer despliegue

1. Confirmar que `ASP-gradexrg-8bed` es F1/Free Linux, que `GradeX-api` es Linux code-only y que el runtime es `DOTNETCORE|10.0`.
2. Verificar que `GradeX-api` ya existe y está Running (el workflow no crea recursos), que tiene identidad `SystemAssigned` exclusiva, HTTPS Only, TLS 1.2+, HTTP/2, FTPS deshabilitado, Always On deshabilitado y los App Settings anteriores.
3. Crear el usuario `[GradeX-api]`, asignar `db_datareader`/`db_datawriter` y añadir reglas de firewall específicas para sus IP de salida. No reutilizar ningún principal de la app eliminada.
4. Configurar las variables OIDC, la credencial federada y el environment `production` con revisores y rama `release`. La identidad OIDC de GitHub Actions es distinta de la identidad administrada de la app.
5. Revisar y publicar los workflows en `release` mediante el procedimiento de Git autorizado. El `release` actual (`37ed258`, commit inicial) no contiene el workflow .NET; no se puede desplegar hasta actualizarlo.
6. Ejecutar `Verify GradeX.Api Azure configuration (manual)` con `VERIFY` para confirmar el preflight sin desplegar. Si falla, corregir identidad/settings/cadena/firewall antes de continuar.
7. Ejecutar quality y lanzar `Deploy GradeX.Api to Azure App Service (manual)` con `DEPLOY`, aprobar el environment y verificar el smoke de `/api/health`.
8. Conservar el ZIP inicial validado y, solo después de que el primer despliegue .NET sea exitoso, probar el rollback con ese run o con un run .NET posterior. No hay rollback a Go.

## Seguridad y mantenimiento

- No versionar credenciales, tokens, certificados privados ni cadenas de conexión con contraseña.
- `.vs/`, `*.user`, `bin/`, `obj/`, `TestResults/`, `.env`, `*.pfx`, `*.snk`, `*.mdf`, `*.ldf`, `*.bak` y `artifacts/` están excluidos por `.gitignore`.
- La autenticación de GitHub Actions con Azure usa OIDC; no se usan secretos de Azure de larga duración.
- La identidad administrada del App Service solo recibe permisos de lectura y escritura sobre la base; no se le conceden permisos de administración.
- No operar sobre bases de producción ni sobre infraestructura de Azure desde el entorno local.
- Mantener las migraciones históricas sin ediciones manuales.
- Revisar el diff antes de cada commit y publicar únicamente archivos técnicos.
