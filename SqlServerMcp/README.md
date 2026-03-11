# SqlServerMcp

Servidor MCP para administrar SQL Server con `.NET 10` en un único proyecto, con soporte `HTTP` y `stdio`, respuestas homogéneas en JSON y `connectionString` obligatoria en cada herramienta.

## Herramientas MCP

- `get_database_info`
- `list_schemas`
- `list_tables`
- `get_table_details`
- `get_object_definition`
- `query_sql`
- `preview_data_change`
- `execute_data_change`
- `create_table`
- `alter_table`
- `create_login`
- `create_user`
- `grant_role_membership`
- `execute_admin_sql`

## Reglas operativas

- Cada llamada debe enviar `connectionString`.
- Todas las respuestas devuelven un objeto JSON con `ok`, `operation`, `data`, `target`, `paging`, `metrics` y `error` cuando aplica.
- Cuando una herramienta falla, la respuesta también incluye `errorType` y `errorDetail` con la excepción completa en crudo.
- `query_sql` sólo acepta una sentencia `SELECT` o `WITH`.
- `preview_data_change` y `execute_data_change` sólo aceptan `UPDATE` o `DELETE` simples sobre un único objetivo.
- Si una sentencia DML no incluye `WHERE`, debes enviar `allowAffectAllRows=true`.
- Por defecto `execute_data_change` exige `previewToken`; puede desactivarse con `SqlServerMcp__SkipDmlConfirmation=true`.
- `pageSize` por defecto es `"25"`. Usa `"0"` o `"*"` para devolver todo.

## Configuración

`appsettings.json`

```json
{
  "SqlServerMcp": {
    "HttpPath": "/mcp",
    "SkipDmlConfirmation": false,
    "PreviewSampleLimit": 10
  }
}
```

Variables de entorno equivalentes:

- `SqlServerMcp__HttpPath`
- `SqlServerMcp__SkipDmlConfirmation`
- `SqlServerMcp__PreviewSampleLimit`

## Ejecución

HTTP:

```bash
dotnet run
```

`stdio`:

```bash
dotnet run -- --transport stdio
```

## Integración MCP

Ejemplo HTTP para clientes MCP:

```json
{
  "servers": {
    "SqlServerMcp": {
      "type": "http",
      "url": "http://localhost:6191/mcp"
    }
  }
}
```

Ejemplo `stdio`:

```json
{
  "servers": {
    "SqlServerMcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:\\tfs\\Owner\\CreatingMCPs\\SqlServerMcp\\SqlServerMcp\\SqlServerMcp.csproj",
        "--",
        "--transport",
        "stdio"
      ]
    }
  }
}
```

## Codex CLI

Codex CLI lee los MCPs desde `~/.codex/config.toml`.

### Usando `SqlServerMcp` como `stdio`

Si quieres que Codex lance este MCP directamente, configúralo así:

```toml
[mcp_servers.SqlServerMcp]
command = "dotnet"
args = [
  "run",
  "--project",
  "D:/tfs/Owner/CreatingMCPs/SqlServerMcp/SqlServerMcp/SqlServerMcp.csproj",
  "--",
  "--transport",
  "stdio"
]
startup_timeout_sec = 30.0

[mcp_servers.SqlServerMcp.env]
SqlServerMcp__SkipDmlConfirmation = "true"
SqlServerMcp__PreviewSampleLimit = "10"
```

`SqlServerMcp__SkipDmlConfirmation = "true"` desactiva la exigencia de `previewToken` para `UPDATE` y `DELETE`.

Si quieres mantener el comportamiento seguro por defecto:

```toml
[mcp_servers.SqlServerMcp.env]
SqlServerMcp__SkipDmlConfirmation = "false"
```

El valor debe ir dentro de `[mcp_servers.SqlServerMcp.env]` y como string.

Si sólo quieres configurar esa variable:

```toml
[mcp_servers.SqlServerMcp.env]
SqlServerMcp__SkipDmlConfirmation = "true"
```

En modo `stdio` también podrías definir:

- `SqlServerMcp__PreviewSampleLimit`
- `SqlServerMcp__HttpPath`

Pero `SqlServerMcp__HttpPath` no tiene efecto práctico en `stdio`, porque el servidor no expone HTTP en ese modo.

### Usando `SqlServerMcp` como HTTP remoto

Si ya tienes el servidor levantado en HTTP, la entrada en `config.toml` sería:

```toml
[mcp_servers.SqlServerMcp]
url = "http://localhost:6191/mcp"
startup_timeout_sec = 30.0
```

En este caso Codex no arranca el proceso del servidor, así que `SqlServerMcp__SkipDmlConfirmation`, `SqlServerMcp__HttpPath` y `SqlServerMcp__PreviewSampleLimit` no se pasan desde `config.toml` del MCP. Debes configurarlos en el proceso que ejecuta el servidor HTTP.

Ejemplo en PowerShell antes de arrancar el servidor:

```powershell
$env:SqlServerMcp__HttpPath = "/mcp"
$env:SqlServerMcp__SkipDmlConfirmation = "false"
$env:SqlServerMcp__PreviewSampleLimit = "10"
dotnet run --launch-profile http
```

### Usando `SqlServerMcp` contra IIS Express local

El repositorio queda preparado con un perfil `IIS Express` en `launchSettings.json` para desarrollo local.

URL esperada en local:

```text
http://localhost:18080/mcp
```

Entrada de Codex CLI para ese caso:

```toml
[mcp_servers.SqlServerMcp]
url = "http://localhost:18080/mcp"
startup_timeout_sec = 30.0
```

Notas importantes:

- IIS Express usa su propio módulo ASP.NET Core local y no requiere el Hosting Bundle de IIS real.
- Este camino está pensado para desarrollo/pruebas en Windows Home o máquinas sin IIS completo.
- Para arrancarlo como `IIS Express`, usa el perfil correspondiente desde Visual Studio.

### Alta rápida con Codex CLI

También puedes registrarlo con comandos:

```powershell
codex mcp add SqlServerMcp -- dotnet run --project D:/tfs/Owner/CreatingMCPs/SqlServerMcp/SqlServerMcp/SqlServerMcp.csproj -- --transport stdio
```

```powershell
codex mcp add SqlServerMcp --url http://localhost:6191/mcp
```

## Despliegue en IIS

Sí, este MCP puede publicarse en IIS, pero sólo en modo HTTP. El transporte `stdio` no aplica a IIS.

### Qué pasa con `web.config`

No hace falta mantener un `web.config` manual dentro del repositorio para este proyecto. Al publicar una app ASP.NET Core para IIS, el SDK genera el `web.config` en la salida de `publish`. Ese archivo generado sí debe desplegarse junto con la aplicación.

### Requisito real del servidor

La publicación está preparada como **self-contained** en `win-x64`, así que el servidor no necesita tener instalado el runtime de `.NET 10`.

Eso no elimina este requisito:

- IIS sí necesita tener instalado el **ASP.NET Core Hosting Bundle** para disponer del ASP.NET Core Module que arranca la aplicación detrás de IIS.

Esto aplica a **IIS real**. No aplica a **IIS Express**, que trae su propio módulo local.

### Perfiles de publicación preparados

Se han dejado dos perfiles en `Properties/PublishProfiles`:

- `SqlServerMcp-IIS-Folder.pubxml`
- `SqlServerMcp-IIS-WebDeploy.pubxml`

Ambos publican en:

- `Release`
- `net10.0`
- `win-x64`
- self-contained
- no single-file

### Publicación a carpeta

Para generar la salida lista para copiar al servidor IIS:

```powershell
dotnet publish .\SqlServerMcp.csproj /p:PublishProfile=SqlServerMcp-IIS-Folder
```

La salida queda en:

```text
bin\Release\net10.0\win-x64\publish\iis-folder\
```

Esa carpeta contendrá el ejecutable, `appsettings.json` y el `web.config` generado para IIS.

Importante: IIS no soporta despliegues ASP.NET Core `single-file`. Para IIS, este proyecto se publica como `self-contained` pero con múltiples archivos.

### Publicación Web Deploy

También hay un perfil `SqlServerMcp-IIS-WebDeploy.pubxml`, pero antes de usarlo debes rellenar:

- `MSDeployServiceURL`
- `DeployIisAppPath`
- `UserName`
- `Password`

Después podrás publicar con:

```powershell
dotnet publish .\SqlServerMcp.csproj /p:PublishProfile=SqlServerMcp-IIS-WebDeploy
```

### URL esperada en IIS

La preparación está pensada para desplegarse como sitio raíz en IIS. En ese caso, si dejas el valor por defecto:

```json
{
  "SqlServerMcp": {
    "HttpPath": "/mcp"
  }
}
```

la URL del endpoint MCP será:

```text
https://tu-servidor/mcp
```

### Variables útiles en IIS

Puedes configurar estas variables en el proceso hospedado por IIS, en `web.config`, variables del sistema o la configuración del app pool:

- `SqlServerMcp__HttpPath`
- `SqlServerMcp__SkipDmlConfirmation`
- `SqlServerMcp__PreviewSampleLimit`

### Seguridad en IIS

El endpoint HTTP sigue sin autenticación propia. Para esta versión, trátalo como servicio de uso interno o protégelo con controles externos de red, reverse proxy o autenticación de IIS delante de la app.

## Seguridad

- El transporte HTTP está sin autenticación por diseño. Úsalo sólo en entornos internos o locales.
- El permiso efectivo lo determina la propia `connectionString` enviada por el cliente.
- El servidor no persiste cadenas de conexión; se usan sólo para la llamada actual.
