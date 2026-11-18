begin;

alter table edenpos.settings
    add column employee_name text,
    add column next_receipt_number integer not null default 1 check (next_receipt_number > 0),
    add column next_shift_number integer not null default 1 check (next_shift_number > 0),
    add column inventory_schema_version integer not null default 0,
    add column catalog_schema_version integer not null default 0,
    add column security_schema_version integer not null default 0;

alter table edenpos.products add column display_order integer not null default 0;
alter table edenpos.product_options add column display_order integer not null default 0;

create table edenpos.app_users (
    id text primary key,
    name text not null,
    role text not null,
    pin_salt text not null,
    pin_hash text not null,
    active boolean not null,
    created_at timestamptz not null,
    updated_at timestamptz not null
);
alter table edenpos.app_users enable row level security;
revoke all on edenpos.app_users from public, anon, authenticated;

commit;
