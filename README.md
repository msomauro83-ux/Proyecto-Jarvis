# Proyecto Jarvis

Jarvis · Vale 0.4.4 para Windows. Asistente de comandos de voz local.

En **Ajustes**, activa «Iniciar en la bandeja al entrar a Windows con el micrófono activo» y guarda. En el siguiente inicio de sesión, Jarvis se abrirá en la bandeja y empezará a escuchar. Puedes pausar o reactivar el micrófono desde el icono V o con **Ctrl + Alt + V**. Al abrirlo manualmente desde su acceso directo, el micrófono comienza en pausa.

Las confirmaciones habladas usan **Microsoft Sabina Desktop, español de México (es-MX)** cuando está instalada y habilitada. En otro equipo, Jarvis usa una voz disponible de español de México, luego otra de español y finalmente la voz predeterminada de Windows. La opción «Confirmar acciones con la voz local de Windows» debe estar activada. Esta selección solo afecta a Jarvis.

Para comprobarlo, inicia Jarvis, activa el micrófono y di **«Vale, me escuchas»**. Debe responder con Sabina. Si no reconoce la voz, revisa el dispositivo de entrada, los permisos de micrófono de Windows y la sección **Actividad**. Si no responde en voz alta, revisa esa opción en Ajustes, el volumen y el dispositivo de salida de Windows.

El inicio automático usa `"ruta\Jarvis.exe" --tray --listen`. Si el micrófono aún no está listo, Jarvis hace hasta tres intentos, separados por cinco segundos. Pausar o salir cancela los reintentos. Al guardar Ajustes se actualizan también los registros de inicio de versiones anteriores que abrían Jarvis con el micrófono en pausa. Suspender, bloquear o apagar el equipo conservan sus controles actuales.

Para compilar y verificar:

```powershell
.\test.ps1
.\installer.ps1
.\package.ps1 -SkipBuild
```

El instalador se genera en `artifacts\Jarvis-Vale-Setup-0.4.4.exe` y el ejecutable en `dist\Jarvis.exe`. Las pruebas comprueban el comando de inicio, la selección de voz, los comandos existentes y la interfaz; no reinician Windows, no emiten voz ni ejecutan acciones del PC.

