# Contribuir

Para comunicar un problema, abre una incidencia con la versión de la GUI, versión exacta de openSeaChest, Windows, tipo de conexión y pasos para reproducirlo. Oculta números de serie, nombres de usuario, rutas personales y contraseñas antes de adjuntar registros.

Para proponer un cambio:

1. Crea una rama a partir del código actual.
2. Describe el problema y mantén el cambio centrado en resolverlo.
3. Compila con las referencias de .NET Framework de Microsoft; evita APIs exclusivas de Mono.
4. Ejecuta las pruebas que correspondan y describe cuáles realizaste y en qué entorno.
5. Abre una pull request explicando el cambio, el resultado esperado y sus límites.

No introduzcas operaciones de disco reales en las pruebas automáticas. Mantén la selección explícita de una unidad, el escape de argumentos, la comprobación de identidad y las confirmaciones de operaciones avanzadas. Los cambios en estas protecciones deben explicarse y revisarse de forma explícita.

La fuente de la guía es `docs/guia.json`: genera desde ella el contenido integrado, el TXT y el PDF. Conserva los avisos de terceros y revisa visualmente el PDF si lo regeneras.

Al contribuir código o documentación original, acepta su distribución bajo la licencia MIT del proyecto. No añadas materiales de terceros sin identificar su origen y sus condiciones.
