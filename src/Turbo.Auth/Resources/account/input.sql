-- 首个管理员推荐使用 create-admin.sql，填写用户名/邮箱后通过邮件设置密码，无需复制哈希。
-- 在交互式终端运行下列命令生成 PasswordHash，密码不会回显：
-- dotnet run --project src/Turbo.Auth/Turbo.Auth.csproj -- --hash-password
-- 将生成的哈希替换到 @password_hash；不要将明文密码写入 SQL 文件。

SET @username = 'new-user';
SET @email = 'new-user@example.com';
SET @password_hash = 'REPLACE_WITH_PASSWORD_HASH';

INSERT INTO Accounts (Username, Password, Email, SecurityStamp)
VALUES (@username, @password_hash, @email, REPLACE(UUID(), '-', ''));

SET @account_id = LAST_INSERT_ID();

-- 为普通用户分配 user 角色。
INSERT INTO AccountRoles (AccountId, RoleId)
SELECT @account_id, RoleId
FROM Roles
WHERE Name = 'user';

-- 创建首个管理员时，额外执行以下语句：
-- INSERT INTO AccountRoles (AccountId, RoleId)
-- SELECT @account_id, RoleId
-- FROM Roles
-- WHERE Name IN ('admin', 'vip');
