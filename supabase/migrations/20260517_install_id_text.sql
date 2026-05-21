do $$
declare
    installation_install_id_type text;
    reference_row record;
begin
    if to_regclass('public.installations') is null then
        raise notice 'public.installations does not exist; skipping install_id type migration.';
        return;
    end if;

    create temporary table tmp_install_id_fk_refs (
        table_schema text not null,
        table_name text not null,
        column_name text not null,
        constraint_name text not null,
        update_rule text not null,
        delete_rule text not null
    ) on commit drop;

    insert into tmp_install_id_fk_refs (
        table_schema,
        table_name,
        column_name,
        constraint_name,
        update_rule,
        delete_rule
    )
    select
        tc.table_schema,
        tc.table_name,
        kcu.column_name,
        tc.constraint_name,
        rc.update_rule,
        rc.delete_rule
    from information_schema.table_constraints tc
    join information_schema.key_column_usage kcu
      on tc.constraint_name = kcu.constraint_name
     and tc.table_schema = kcu.table_schema
     and tc.table_name = kcu.table_name
    join information_schema.constraint_column_usage ccu
      on tc.constraint_name = ccu.constraint_name
     and tc.table_schema = ccu.table_schema
    join information_schema.referential_constraints rc
      on tc.constraint_name = rc.constraint_name
     and tc.table_schema = rc.constraint_schema
    where tc.constraint_type = 'FOREIGN KEY'
      and ccu.table_schema = 'public'
      and ccu.table_name = 'installations'
      and ccu.column_name = 'install_id';

    for reference_row in
        select *
        from tmp_install_id_fk_refs
    loop
        execute format(
            'alter table %I.%I drop constraint %I',
            reference_row.table_schema,
            reference_row.table_name,
            reference_row.constraint_name);
    end loop;

    select data_type
    into installation_install_id_type
    from information_schema.columns
    where table_schema = 'public'
      and table_name = 'installations'
      and column_name = 'install_id';

    if installation_install_id_type is not null
       and installation_install_id_type <> 'text' then
        execute 'alter table public.installations alter column install_id type text using install_id::text';
    end if;

    for reference_row in
        select *
        from tmp_install_id_fk_refs
    loop
        if exists (
            select 1
            from information_schema.columns
            where table_schema = reference_row.table_schema
              and table_name = reference_row.table_name
              and column_name = reference_row.column_name
              and data_type <> 'text')
        then
            execute format(
                'alter table %I.%I alter column %I type text using %I::text',
                reference_row.table_schema,
                reference_row.table_name,
                reference_row.column_name,
                reference_row.column_name);
        end if;

        execute format(
            'alter table %I.%I add constraint %I foreign key (%I) references public.installations(install_id) on update %s on delete %s',
            reference_row.table_schema,
            reference_row.table_name,
            reference_row.constraint_name,
            reference_row.column_name,
            reference_row.update_rule,
            reference_row.delete_rule);
    end loop;
end $$;
