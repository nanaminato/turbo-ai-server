-- Back up the database before running this migration.
-- Existing bindings keep their current behaviour: the logical model value is copied
-- into ProviderModelValue and every route starts at priority 0.

ALTER TABLE ModelKeyBinds
    ADD COLUMN ProviderModelValue varchar(200) NULL AFTER Fee,
    ADD COLUMN Priority int NOT NULL DEFAULT 0 AFTER ProviderModelValue;

UPDATE ModelKeyBinds AS binding
INNER JOIN Models AS model ON model.ModelId = binding.ModelId
SET binding.ProviderModelValue = model.ModelValue
WHERE binding.ProviderModelValue IS NULL OR binding.ProviderModelValue = '';

ALTER TABLE ModelKeyBinds
    MODIFY COLUMN ProviderModelValue varchar(200) NOT NULL;

CREATE INDEX IX_ModelKeyBinds_Routing
    ON ModelKeyBinds (ModelId, Enable, Priority);
