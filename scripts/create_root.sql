INSERT INTO public.collections 
    (id, slug, use_path, label, created, modified, created_by, modified_by, is_storage_collection, is_public, customer_id) 
VALUES
    ('root', '', false, '{"en": ["(repository root)"]}', now(), now(), 'Admin', 'Admin', true, true, 1);