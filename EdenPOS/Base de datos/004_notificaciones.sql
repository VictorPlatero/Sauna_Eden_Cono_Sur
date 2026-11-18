begin;

create or replace function edenpos.notificar_cambios()
returns trigger
language plpgsql
set search_path = pg_catalog
as $$
begin
    perform pg_catalog.pg_notify('edenpos_cambios', '');
    return null;
end;
$$;

do $$
declare
    tabla text;
begin
    foreach tabla in array array['app_users', 'cash_movements', 'customers', 'inventory_movements',
        'product_options', 'products', 'receipt_lines', 'receipts', 'saved_ticket_lines',
        'saved_ticket_lockers', 'saved_tickets', 'settings', 'shifts']
    loop
        execute format(
            'create or replace trigger notificar_cambios after insert or update or delete or truncate on edenpos.%I for each statement execute function edenpos.notificar_cambios()',
            tabla);
    end loop;
end;
$$;

revoke all on function edenpos.notificar_cambios() from public, anon, authenticated;

commit;
