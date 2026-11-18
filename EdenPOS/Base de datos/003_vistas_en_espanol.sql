begin;

-- Vistas privadas de consulta. Los permisos de las tablas se aplican al usuario que consulta.
create schema if not exists edenpos_es;
revoke all on schema edenpos_es from public, anon, authenticated;

create or replace view edenpos_es.usuarios with (security_invoker = true) as
select id,
       name as nombre,
       role as rol,
       active as activo,
       created_at as creado_en,
       updated_at as actualizado_en
from edenpos.app_users;

create or replace view edenpos_es.movimientos_caja with (security_invoker = true) as
select id,
       shift_id as turno_id,
       created_at as fecha,
       employee as empleado,
       type as tipo,
       amount as importe,
       comment as comentario
from edenpos.cash_movements;

create or replace view edenpos_es.clientes with (security_invoker = true) as
select dni,
       full_name as nombre_completo,
       source as origen,
       created_at as creado_en,
       updated_at as actualizado_en
from edenpos.customers;

create or replace view edenpos_es.movimientos_inventario with (security_invoker = true) as
select id,
       product_id as producto_id,
       option_id as variante_id,
       product_name as producto,
       created_at as fecha,
       type as tipo,
       quantity as cantidad,
       previous_stock as stock_anterior,
       new_stock as stock_nuevo,
       note as nota,
       reference_id as referencia_id
from edenpos.inventory_movements;

create or replace view edenpos_es.variantes_producto with (security_invoker = true) as
select product_id as producto_id,
       id,
       name as nombre,
       price as precio,
       track_inventory as controla_inventario,
       stock,
       initial_stock as stock_inicial,
       display_order as orden
from edenpos.product_options;

create or replace view edenpos_es.productos with (security_invoker = true) as
select id,
       name as nombre,
       category as categoria,
       price as precio,
       image_file as archivo_imagen,
       color_hex,
       active as activo,
       track_inventory as controla_inventario,
       stock,
       initial_stock as stock_inicial,
       display_order as orden
from edenpos.products;

create or replace view edenpos_es.detalle_ventas with (security_invoker = true) as
select receipt_id as venta_id,
       line_number as numero_linea,
       product_id as producto_id,
       option_id as variante_id,
       name as nombre,
       comment as comentario,
       unit_price as precio_unitario,
       quantity as cantidad,
       line_total as subtotal
from edenpos.receipt_lines;

create or replace view edenpos_es.ventas with (security_invoker = true) as
select id,
       number as numero,
       shift_id as turno_id,
       created_at as fecha,
       employee as empleado,
       customer_name as cliente,
       customer_dni as dni_cliente,
       payment_method as medio_pago,
       amount_received as importe_recibido,
       cash_amount as importe_efectivo,
       yape_amount as importe_yape,
       total,
       status as estado,
       refunded_at as fecha_reembolso,
       refund_reason as motivo_reembolso
from edenpos.receipts;

create or replace view edenpos_es.detalle_tickets with (security_invoker = true) as
select ticket_id,
       line_number as numero_linea,
       product_id as producto_id,
       option_id as variante_id,
       name as nombre,
       comment as comentario,
       unit_price as precio_unitario,
       quantity as cantidad,
       line_total as subtotal
from edenpos.saved_ticket_lines;

create or replace view edenpos_es.candados_tickets with (security_invoker = true) as
select ticket_id,
       locker_number as numero_candado
from edenpos.saved_ticket_lockers;

create or replace view edenpos_es.tickets_guardados with (security_invoker = true) as
select id,
       name as nombre,
       customer_name as cliente,
       customer_dni as dni_cliente,
       adult_count as adultos,
       child_count as ninos,
       created_at as creado_en,
       updated_at as actualizado_en
from edenpos.saved_tickets;

create or replace view edenpos_es.configuracion with (security_invoker = true) as
select store_name as nombre_negocio,
       employee_name as empleado,
       currency_symbol as simbolo_moneda,
       dni_lookup_enabled as consulta_dni_activa,
       next_receipt_number as siguiente_numero_venta,
       next_shift_number as siguiente_numero_turno
from edenpos.settings;

create or replace view edenpos_es.turnos_caja with (security_invoker = true) as
select id,
       number as numero,
       employee as empleado,
       opened_at as apertura,
       closed_at as cierre,
       opening_amount as fondo_inicial,
       closing_amount as importe_cierre,
       expected_amount as importe_esperado,
       is_open as abierto
from edenpos.shifts;

revoke all on all tables in schema edenpos_es from public, anon, authenticated;

commit;
