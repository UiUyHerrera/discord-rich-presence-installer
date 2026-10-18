# Instalador de Estado de Discord

Instalador para Windows de [Estado de Discord](https://github.com/UiUyHerrera/discord-rich-presence-desktop). Es una ventana con un botón: revisa qué falta en la compu, lo instala y deja la app lista con sus accesos directos.

## Qué hace, en orden

1. Busca `winget`. Sin él no puede seguir y te dice cómo conseguirlo.
2. Si faltan Git, Python 3.12 o WebView2 Runtime, los instala con `winget`.
3. Clona la app en `%LOCALAPPDATA%\Programs\EstadoDiscord`. Si ya estaba instalada y abierta, primero la cierra.
4. Crea un entorno virtual de Python e instala las dependencias ahí, sin tocar el Python del sistema.
5. Agrega el acceso directo al menú Inicio y, si marcaste la opción, al escritorio.
6. Si lo pediste, abre la app al terminar.

Cada paso se va mostrando en la ventana, así que si algo falla se ve dónde.

El instalador no pide ni guarda el token de Discord: eso lo hace la app la primera vez que se abre.

## Compilarlo

Está escrito en C# con Windows Forms sobre .NET Framework 4.8, que ya viene con Windows 10 y 11. No hace falta instalar el SDK de .NET:

```bat
build.bat
```

`build.bat` usa el MSBuild que trae el propio .NET Framework y deja el ejecutable en `bin\DiscordRichPresenceInstaller.exe`.

## Archivos

- `Program.cs`: la ventana, con el aviso, las opciones y la barra de progreso.
- `InstallerService.cs`: los pasos de la instalación y los procesos externos (`winget`, `git`, `python`).
