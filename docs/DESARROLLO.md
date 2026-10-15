# Desarrollo y validación

## Estructura

| Ruta | Contenido |
| --- | --- |
| `src/Program.cs` | Ventana principal y operaciones |
| `src/Core.cs` | Análisis de ayuda y discos, argumentos y ejecución |
| `src/WindowsDiskMetadata.cs` | Capacidad y letras mediante WMI |
| `src/GuideUI.cs` | Pestaña y lector de la guía |
| `src/GuideContent.cs` | Guía RTF generada, incluida en la compilación |
| `src/app.manifest` | Solicitud de elevación de administrador |
| `docs/guia.json` | Fuente editable de la guía |
| `tests/` | Pruebas y resultados históricos |
| `LICENCIAS/` | Textos de licencia conservados |

## Compilar en Windows

Requiere el compilador C# de .NET Framework y Windows con .NET Framework 4.8. `COMPILAR.cmd` busca `csc.exe` bajo `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319` o `Framework\v4.0.30319`. La ruta conserva ese nombre aunque el sistema tenga .NET Framework 4.8.

Ejecuta `COMPILAR.cmd` con doble clic. Se genera `SeaChestGUI.exe` junto a su configuración. No necesita descargar paquetes ni regenerar documentación. Si falta el compilador, instala las herramientas de desarrollo de .NET Framework 4.8 de Microsoft y comprueba la ruta.

## Compilar desde Linux

Solo para desarrollo: requiere Mono (`mcs`), Python 3 y las referencias de Microsoft del paquete `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3, extraído aparte. Desde la raíz:

```sh
python3 src/build_linux.py /ruta/al/paquete/build/.NETFramework/v4.8
```

El script compila contra las referencias de Microsoft y verifica el manifiesto. No compiles el producto contra las bibliotecas de ejecución de Mono, que pueden exponer APIs ausentes en Windows.

## Pruebas sin discos

Después de compilar, en Linux con Mono:

```sh
mcs -r:System.Core -r:SeaChestGUI.exe -out:tests/CoreTests.exe tests/CoreTests.cs
MONO_PATH=. mono tests/CoreTests.exe
mcs -r:System.Core -r:SeaChestGUI.exe -out:tests/MetadataTests.exe tests/MetadataTests.cs
MONO_PATH=. mono tests/MetadataTests.exe
mcs -r:System.Core -r:SeaChestGUI.exe -out:tests/ResultsTests.exe tests/ResultsTests.cs
MONO_PATH=. mono tests/ResultsTests.exe
python3 tests/VerifyManifest.py SeaChestGUI.exe
```

`tests/LEEME.txt` explica las comprobaciones adicionales con las ayudas oficiales y el backend simulado. El número de comprobaciones de CoreTests depende de esos argumentos opcionales; el recuento histórico de 65 no corresponde a ejecutarlo sin ellos.

Para la prueba visual, con un escritorio gráfico o Xvfb disponible:

```sh
mcs -r:System.Core -r:System.Windows.Forms -r:System.Drawing -r:SeaChestGUI.exe -out:tests/GuideViewChecks.exe tests/GuideViewChecks.cs
MONO_PATH=. mono tests/GuideViewChecks.exe
```

Esta prueba crea la ventana con datos de demostración, recorre las secciones y abre la lectura ampliada. No sustituye la aceptación en Windows.

## Editar la guía

Edita `docs/guia.json` y conserva los avisos originales en `LICENCIAS`. El generador requiere Python 3, ReportLab y fuentes DejaVu en `/usr/share/fonts/truetype/dejavu`, tal como está escrito para Linux:

```sh
python3 docs/generar_guia.py
```

Actualiza `src/GuideContent.cs`, `docs/GUIA_DE_USO.txt` y el PDF. Recompila la GUI después y revisa las páginas y enlaces del PDF. Para otras plataformas hay que adaptar la ruta de fuentes. Estos requisitos no afectan a quien simplemente compila o usa la aplicación.

## Preparar una versión nueva

Actualiza de forma coherente la versión del ensamblado y los textos en `Program.cs`, la ruta del PDF en `GuideUI.cs`, el nombre y versión del PDF en el generador, la guía JSON, README y CHANGELOG. Busca la versión anterior en el repositorio para no dejar referencias antiguas. Regenera la guía, compila y valida antes de etiquetar una release.

El paquete portable debe contener el EXE, su configuración, la guía PDF, las licencias y las instrucciones. Los ejecutables de openSeaChest se descargan aparte. Genera los paquetes desde el código etiquetado y publica un SHA-256 de los archivos exactos adjuntos. No incluyas registros de discos ni archivos temporales.

## Alcance de la validación publicada

Consulta `tests/RESULTADOS.txt`. Las comprobaciones registradas se hicieron en Linux/Mono con datos simulados y referencias Microsoft. No se certifican todas las operaciones en Windows ni todas las combinaciones de discos, puentes USB y controladoras.

La versión 0.1.9 actualiza la guía integrada, Ayuda y el PDF. Los comandos y protecciones se mantienen. No añade CI ni publicación automática.
