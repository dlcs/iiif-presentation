INSERT INTO public.collections 
	(id, use_path, label, thumbnail, locked_by, created, modified, created_by, modified_by, tags, is_storage_collection, is_public, customer_id) 
VALUES 
	('root', true, '{"en": ["(repository root)"]}', 'some/thumb', null, now(),  now(), 'Admin', 'Admin', null, true, true, 1);

INSERT INTO public.hierarchy 
	(collection_id, manifest_id, type, slug, parent, items_order, canonical, customer_id) 
VALUES 
	('root', null, 0, '', null, null, true, 1);