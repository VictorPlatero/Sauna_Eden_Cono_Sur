begin;

do $$
declare
    table_count integer;
    protected_count integer;
    line_total numeric;
begin
    select count(*), count(*) filter (where c.relrowsecurity)
      into table_count, protected_count
    from pg_catalog.pg_class c
    join pg_catalog.pg_namespace n on n.oid = c.relnamespace
    where n.nspname = 'edenpos' and c.relkind = 'r';

    if table_count <> 13 or protected_count <> 13 then
        raise exception 'Expected 13 tables with RLS, found % tables and % protected',
            table_count, protected_count;
    end if;
    if has_schema_privilege('anon', 'edenpos', 'USAGE')
        or has_schema_privilege('authenticated', 'edenpos', 'USAGE') then
        raise exception 'API roles must not have access to the edenpos schema';
    end if;

    insert into edenpos.shifts (id, number, employee, opening_amount)
    values ('verification-shift', -1, 'Test', 0);
    begin
        insert into edenpos.shifts (id, number, employee, opening_amount)
        values ('verification-second-shift', -2, 'Test', 0);
        raise exception 'A second open shift was accepted';
    exception when unique_violation then
        null;
    end;

    insert into edenpos.receipts (id, number, shift_id, employee, payment_method, total, status)
    values ('verification-receipt', -1, 'verification-shift', 'Test', 'Efectivo', 7.50, 'Pagado');
    insert into edenpos.receipt_lines (receipt_id, line_number, name, unit_price, quantity)
    values ('verification-receipt', 1, 'Test product', 2.50, 3)
    returning receipt_lines.line_total into line_total;
    if line_total <> 7.50 then
        raise exception 'Unexpected receipt line total %', line_total;
    end if;

    insert into edenpos.saved_tickets (id, name)
    values ('verification-ticket', 'Test'), ('verification-second-ticket', 'Test');
    insert into edenpos.saved_ticket_lockers (ticket_id, locker_number)
    values ('verification-ticket', 1);
    begin
        insert into edenpos.saved_ticket_lockers (ticket_id, locker_number)
        values ('verification-second-ticket', 1);
        raise exception 'A duplicate locker was accepted';
    exception when unique_violation then
        null;
    end;
    begin
        insert into edenpos.saved_ticket_lockers (ticket_id, locker_number)
        values ('verification-ticket', 36);
        raise exception 'An out-of-range locker was accepted';
    exception when check_violation then
        null;
    end;
end $$;

rollback;

select 'OK' as verification, count(*) as rls_enabled_tables
from pg_catalog.pg_tables
where schemaname = 'edenpos' and rowsecurity;
