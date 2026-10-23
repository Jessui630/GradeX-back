# GradeX-back

Backend de **GradeX**, una plataforma para la verificación de originalidad de productos. Expone una API REST construida con ASP.NET Core y .NET 10, organizada en capas, con persistencia mediante Entity Framework Core 10 y SQL Server.

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
| Base de datos | SQL Server / SQL Server LocalDB |
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

# Aplicar la migración más reciente en desarrollo
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update `
  --project src/GradeX.Infrastructure `
  --startup-project src/GradeX.Api `
  --context GradeXDbContext
```

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

Las pruebas unitarias no abren SQL Server. La prueba de integración HTTP usa `WebApplicationFactory` y `TestServer`, y tampoco abre SQL Server.

## Pruebas

```powershell
dotnet test GradeX.slnx
```

Pruebas incluidas actualmente:

- Contrato del controlador de salud.
- Contrato de la respuesta de salud y su versión.
- Ruta HTTP `/api/health` mediante `WebApplicationFactory` y `TestServer`, sin SQL Server.

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

`.github/workflows/quality.yml` se ejecuta en `push`, `pull_request` y `workflow_dispatch`. Usa el SDK seleccionado por `global.json`, no necesita LocalDB ni secretos personalizados, aplica el gate estricto y publica logs, SARIF y TRX como artefactos.

## Despliegue

El despliegue actual todavía corresponde al backend Go heredado y no publica `GradeX.Api`:

- `.github/workflows/deploy.yml` solo se ejecuta con `push` a `Sprint1` y publica `ghcr.io/jessui630/gradex-back:sprint1`.
- El `Dockerfile` de la raíz compila `main.go` con Go y arranca `gradex-api`; no incluye el host ASP.NET Core.
- El workflow no despliega en Azure ni configura la base de datos; únicamente publica la imagen.

Para desplegar `GradeX.Api` con ASP.NET Core 10 será necesario:

- Un Dockerfile multi-stage con SDK .NET 10 y runtime ASP.NET Core 10.
- Publicar `src/GradeX.Api`, exponer el puerto `8080` y ejecutar con un usuario sin privilegios.
- Inyectar `ConnectionStrings__GradeX` mediante variables de entorno o secretos; no incluir credenciales en la imagen.
- Aplicar las migraciones EF Core en un paso separado, sin `Migrate()` al arrancar.
- Configurar el healthcheck de la plataforma contra `/api/health`.
- Actualizar `deploy.yml` (rama, registry, tag, entorno y permisos), fijar las acciones por SHA y retirar los artefactos Go cuando el corte esté verificado.
- Excluir archivos locales no técnicos y artefactos de build del contexto Docker.

`deploy.yml` y `Dockerfile` no se modifican en esta corrección.

## Seguridad y mantenimiento

- No versionar credenciales, tokens, certificados privados ni cadenas de conexión con contraseña.
- `.vs/`, `*.user`, `bin/`, `obj/`, `TestResults/`, `.env`, `*.pfx`, `*.snk`, `*.mdf`, `*.ldf` y `*.bak` están excluidos por `.gitignore`.
- No operar sobre bases de producción ni sobre infraestructura de Azure desde el entorno local.
- Mantener las migraciones históricas sin ediciones manuales.
- Revisar el diff antes de cada commit y publicar únicamente archivos técnicos.
