# Edén POS

Aplicación de punto de venta táctil para Windows, inspirada en el flujo de las capturas proporcionadas. Está desarrollada en **C# WinForms para .NET Framework 4.8** y no necesita base de datos: toda la información se conserva en ficheros JSON locales.

El ejecutable y la ventana principal utilizan el logotipo circular del negocio como icono oficial de Edén POS.

## Funciones incluidas

- Catálogo táctil con búsqueda y categorías: Alquileres, Aseo & Ventas, Cafetería y Sauna.
- Inicio de sesión con logo, selector de usuario, PIN protegido y teclado numérico táctil.
- Interfaz adaptable a monitores pequeños, Full HD y 4K, respetando el escalado configurado en Windows.
- Ventanas de variantes para Gaseosas, Hidratantes, Jarras, Sándwiches, Snacks, Acondicionadores, Jabones y Shampoos.
- Gestión organizada para la dueña: productos principales y todas sus variantes visibles, editables y buscables desde Inventario.
- Permisos por rol: la dueña accede a Inventario y Configuración; los trabajadores acceden a Ventas, Recibos y Turno.
- Administración de usuarios desde Configuración, con alta, edición, cambio de PIN, activación y eliminación segura.
- Ticket de venta con cantidades, eliminación de líneas, cliente opcional y total automático.
- Guardado persistente de tickets pendientes, con recuperación y eliminación posterior.
- Actualización del mismo ticket recuperado al agregar nuevos artículos, sin duplicarlo ni bloquear sus propias llaves.
- Selección múltiple táctil entre 35 candados numerados para cada ticket pendiente, evitando números duplicados y permitiendo editar las llaves asignadas.
- Resumen de adultos y niños presentes, con búsqueda de tickets por nombre, DNI o número de candado.
- Resumen visible al recuperar un ticket con el nombre completo del cliente y todos sus candados prestados.
- Aviso obligatorio antes de cobrar para verificar prendas alquiladas y solicitar las llaves.
- Cliente por DNI con autocompletado opcional, registro local y consulta segura mediante token.
- Cobro por Efectivo, Yape o pago Mixto (parte en efectivo y parte por Yape), con cálculo de vuelto.
- Resumen en Recibos con la suma separada de todos los pagos en efectivo y por Yape.
- Recibos agrupados automáticamente por día y resumen de Efectivo/Yape para cualquier fecha seleccionada.
- Recibos persistentes, búsqueda, detalle, exportación a texto y reembolso.
- Apertura y cierre de turno con fondo inicial, efectivo esperado y diferencia de caja.
- Gestión de tesorería con depósitos, pagos/salidas, comentarios e historial.
- Inventario por producto y variante: entradas del día, salidas vendidas y saldo disponible independientes.
- Alta, edición y eliminación segura de artículos, con registro de nuevas entradas de mercadería.
- Descuento automático del saldo del producto o variante al cobrar y reposición automática cuando se reembolsa un recibo.
- Configuración del negocio, usuarios y símbolo de moneda.
- Validaciones de turnos, cantidades, caja, artículos duplicados, clientes y reembolsos.
- Registro de errores técnicos en `errors.log` para facilitar soporte.
- 42 artículos de demostración y 34 imágenes similares incluidas.

## Abrir en Visual Studio

1. Instale Visual Studio 2022 con la carga de trabajo **Desarrollo de escritorio de .NET** y .NET Framework 4.8.
2. Abra `EdenPOS.sln`.
3. Seleccione `Debug` o `Release` y pulse `F5`.

También puede ejecutar directamente `Ejecutable\EdenPOS.exe` en un equipo Windows que tenga .NET Framework 4.8.

La compilación usa **Any CPU**, por lo que funciona como aplicación de 32 bits en Windows x86 y de 64 bits en Windows x64. Se recomienda Windows 10 u 11 con [Microsoft .NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48).

## Primer inicio de sesión

- Usuario inicial: `DUEÑA`
- PIN inicial: `0000`

Al ingresar como dueña, abra **Configuración → Usuarios y permisos**, agregue a los trabajadores y cambie el PIN inicial. Los PIN se almacenan mediante hash con sal y no se guardan como texto visible.

## Archivos de datos

En la primera ejecución se crean estos ficheros:

- `catalog.json`
- `receipts.json`
- `saved_tickets.json`
- `customers.json`
- `shifts.json`
- `cash_movements.json`
- `inventory_movements.json`
- `settings.json`

Ubicación predeterminada:

`%LOCALAPPDATA%\EdenPOS\Data`

La distribución incluida en `Ejecutable` contiene un archivo `data.path` y utiliza exclusivamente la carpeta `Base de datos` situada en la raíz del proyecto. El archivo permite definir una ruta relativa sin fijar el nombre del usuario ni la letra del disco.

La pantalla **Configuración** incluye un botón para abrir esa carpeta. Para pruebas o instalaciones especiales puede cambiarse la ubicación mediante la variable de entorno `EDENPOS_DATA_PATH`.

## Consulta de DNI y nombre

La consulta en línea es opcional. Edén POS no raspa la página de RENIEC ni incorpora claves ajenas.

1. Cree una cuenta y obtenga un token en [ApiPeruDev](https://apiperu.dev/).
2. Abra **Configuración → Consulta de clientes por DNI**.
3. Ingrese el token, active la consulta automática y guarde.
4. En **Ventas**, escriba los 8 dígitos del DNI. Si existe en la fuente, aparecerá el nombre completo.

El proveedor declara que no se conecta directamente con RENIEC: usa el padrón reducido SUNAT y otras fuentes públicas. Algunos DNI, especialmente menores de edad, pueden no devolver resultados. Siempre debe verificarse el DNI físico del cliente.

El token se cifra mediante la protección de datos de la cuenta actual de Windows. Los clientes consultados se guardan en `customers.json` para futuras búsquedas sin internet y pueden eliminarse desde Configuración. Alternativamente, un administrador puede proporcionar el token mediante `EDENPOS_DNI_TOKEN`.

## Llevar Edén POS a otra computadora

### Opción recomendada: copia de seguridad

1. En la PC actual abra **Configuración → Exportar copia**.
2. Guarde el archivo `.edenbackup` y cópielo junto con la carpeta `Ejecutable`.
3. En la nueva PC ejecute `EdenPOS.exe`.
4. Abra **Configuración → Importar copia** y seleccione el archivo.
5. Ingrese nuevamente el token de DNI y guarde.

La copia incluye catálogo, productos, imágenes personalizadas, recibos, turnos, movimientos, clientes y tickets guardados. El token DNI se excluye intencionalmente porque está cifrado para la cuenta original de Windows.

### Opción USB: modo portable

Desde Configuración puede activar **Modo portable**. Los datos y las imágenes se copiarán a una carpeta visible `BaseDatos` junto al ejecutable, permitiendo trasladar la carpeta completa o trabajar desde una memoria USB. Los ficheros `.json` son archivos de texto y funcionan como la base de datos local temporal del programa.

- No active el modo portable dentro de `Archivos de programa`, porque normalmente no permite escritura.
- Cierre Edén POS antes de copiar la carpeta.
- En otra cuenta de Windows deberá ingresar nuevamente el token DNI.
- Las imágenes añadidas por el usuario utilizan rutas portables y no dependen de la letra del disco.

## Atajos

- `F2`: Ventas
- `F3`: Recibos
- `F4`: Turno
- `F5`: Actualizar pantalla

## Recursos gráficos

Los iconos de catálogo proceden de Twemoji y están distribuidos bajo CC BY 4.0. Consulte `THIRD_PARTY_NOTICES.txt` y `Assets\Catalog\SOURCES.md`.

Edén POS es una implementación independiente y no está afiliada a Loyverse.
