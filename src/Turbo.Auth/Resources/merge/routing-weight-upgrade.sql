-- Run once after architecture-upgrade.sql, after backup. NULL preserves inverse-Fee routing.
ALTER TABLE ModelKeyBinds ADD COLUMN RoutingWeight double NULL AFTER Fee;
