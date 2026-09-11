<#
    publish.ps1 — Publica RH Manager listo para entregar.

    Uso:
        .\publish.ps1                 limpia, publica y verifica
        .\publish.ps1 -SinLimpiar     publica sin borrar bin y obj (mas rapido)
        .\publish.ps1 -Empaquetar     ademas arma publicado\ con carpeta fechada y zip

    Lo que NO hace, y por que:

    · No copia ningun archivo .db al publicado. RH Manager guarda su base en
      %LOCALAPPDATA%\RH Manager\rhmanager.db, nunca junto al ejecutable: la
      carpeta de instalacion puede ser de solo lectura (Configuracion\RutasRhManager.cs).
      La base se crea sola en el primer arranque, aplicando las migraciones
      (Servicios\ServicioDiagnostico.cs). Copiar la base de desarrollo ademas
      mandaria al cliente los expedientes reales que haya en esta maquina.

    · No copia appsettings.Local.json. Es opcional a proposito
      (MauiProgram.CargarConfiguracion lo lee con optional: true) y es el
      archivo donde cada quien pone su configuracion de maquina.
#>
[CmdletBinding()]
param(
    [string] $Configuracion = 'Release',
    [string] $Runtime       = 'win-x64',
    [switch] $SinLimpiar,
    [switch] $Empaquetar
)

$ErrorActionPreference = 'Stop'

$raiz     = Split-Path -Parent $MyInvocation.MyCommand.Path
$proyecto = Join-Path $raiz 'empleados\empleados.csproj'
$marco    = 'net10.0-windows10.0.19041.0'
$carpetaBin     = Join-Path $raiz "empleados\bin\$Configuracion\$marco\$Runtime"
$carpetaPublish = Join-Path $carpetaBin 'publish'

if (-not (Test-Path $proyecto)) { throw "No encuentro el proyecto en $proyecto" }

function Escribir-Paso([string] $texto) {
    Write-Host ''
    Write-Host "==> $texto" -ForegroundColor Cyan
}

# ---------------------------------------------------------------- 1. Limpieza
if ($SinLimpiar) {
    Escribir-Paso 'Limpieza omitida (-SinLimpiar)'
} else {
    Escribir-Paso 'Limpiando bin y obj'
    foreach ($c in @('bin', 'obj')) {
        $ruta = Join-Path $raiz "empleados\$c"
        if (Test-Path $ruta) {
            Remove-Item $ruta -Recurse -Force
            Write-Host "    borrado  empleados\$c"
        }
    }
}

# -------------------------------------------------------------- 2. Publicacion
Escribir-Paso "Publicando ($Configuracion / $Runtime / autocontenido)"

# Se le pasa el .csproj y no la solucion: con la solucion, -o dispara NETSDK1194.
& dotnet publish $proyecto -c $Configuracion -r $Runtime --self-contained true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish termino con codigo $LASTEXITCODE" }
if (-not (Test-Path $carpetaPublish)) { throw "No aparecio la carpeta $carpetaPublish" }

# ------------------------------------------------------------ 3. Verificacion
Escribir-Paso 'Verificando el publicado'

$problemas = New-Object System.Collections.Generic.List[string]

# 3.1 El ejecutable y la configuracion obligatoria tienen que estar.
foreach ($obligatorio in @('empleados.exe', 'appsettings.json')) {
    $f = Join-Path $carpetaPublish $obligatorio
    if (Test-Path $f) {
        Write-Host "    OK     $obligatorio"
    } else {
        $problemas.Add("Falta $obligatorio en el publicado.")
    }
}

# 3.2 Ninguna base de datos viaja en el publicado.
$bases = Get-ChildItem $carpetaPublish -Filter '*.db' -Recurse -ErrorAction SilentlyContinue
if ($bases) {
    foreach ($b in $bases) {
        $problemas.Add("Hay una base de datos en el publicado: $($b.Name). Sacarla: la app usa %LOCALAPPDATA% y esto puede filtrar datos reales.")
    }
} else {
    Write-Host '    OK     ningun .db viaja en el publicado'
}

# 3.3 Las dll del publicado deben ser LAS MISMAS que las de bin.
#
#     Si difieren, ReadyToRun se volvio a encender y el publicado lleva
#     binarios recompilados, unicos y sin reputacion. En una maquina con el
#     Control de aplicaciones de Windows encendido (Smart App Control, por
#     omision en Windows 11 nuevo) eso los bloquea con 0x800711C7 y la app
#     falla al autenticar. Ver el comentario de PublishReadyToRun en el .csproj.
$recompiladas = New-Object System.Collections.Generic.List[string]
foreach ($dll in Get-ChildItem $carpetaPublish -Filter '*.dll' -File) {
    $gemela = Join-Path $carpetaBin $dll.Name
    if (-not (Test-Path $gemela)) { continue }
    if ((Get-FileHash $dll.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $gemela -Algorithm SHA256).Hash) {
        $recompiladas.Add($dll.Name)
    }
}
if ($recompiladas.Count -gt 0) {
    $problemas.Add("$($recompiladas.Count) ensamblado(s) del publicado no coinciden con los de bin (ReadyToRun encendido). Ejemplos: $(($recompiladas | Select-Object -First 5) -join ', ')")
} else {
    Write-Host '    OK     los ensamblados del publicado son identicos a los de bin'
}

# 3.4 Aviso de firma: sin firmar, cualquier maquina con Smart App Control puede
#     rechazar el ejecutable. No es un fallo del publicado, es del entorno.
Write-Host '    AVISO  el publicado no esta firmado. En Windows con Smart App Control'
Write-Host '           encendido, el cliente puede necesitar apagarlo, o hay que'
Write-Host '           firmar los binarios con un certificado de firma de codigo.'

$archivos = (Get-ChildItem $carpetaPublish -Recurse -File)
$megas = [math]::Round(($archivos | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
Write-Host ''
Write-Host "    $($archivos.Count) archivos, $megas MB"
Write-Host "    $carpetaPublish"

# ------------------------------------------------------------- 4. Empaquetado
if ($Empaquetar) {
    Escribir-Paso 'Armando la carpeta publicado\'

    $destino = Join-Path $raiz 'publicado'
    $fecha   = Get-Date -Format 'yyyy-MM-dd'
    $carpetaApp    = Join-Path $destino 'RH Manager'
    $carpetaFecha  = Join-Path $destino "RH-Manager-$fecha"

    New-Item -ItemType Directory -Force -Path $destino | Out-Null
    if (Test-Path $carpetaApp) { Remove-Item $carpetaApp -Recurse -Force }
    Copy-Item $carpetaPublish $carpetaApp -Recurse

    if (Test-Path $carpetaFecha) { Remove-Item $carpetaFecha -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $carpetaFecha | Out-Null
    Copy-Item $carpetaApp (Join-Path $carpetaFecha 'RH Manager') -Recurse
    $leeme = Join-Path $destino 'LEEME.txt'
    if (Test-Path $leeme) { Copy-Item $leeme $carpetaFecha }

    $zip = "$carpetaFecha.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path $carpetaFecha -DestinationPath $zip
    Write-Host "    $carpetaApp"
    Write-Host "    $zip"
}

# ------------------------------------------------------------------ Resultado
Write-Host ''
if ($problemas.Count -gt 0) {
    Write-Host 'PUBLICADO CON PROBLEMAS' -ForegroundColor Red
    foreach ($p in $problemas) { Write-Host "  · $p" -ForegroundColor Red }
    exit 1
}

Write-Host 'PUBLICADO CORRECTO' -ForegroundColor Green
exit 0
