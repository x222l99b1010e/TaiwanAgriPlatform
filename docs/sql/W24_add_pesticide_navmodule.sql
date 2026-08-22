-- W24 農藥查詢：正式 DB 新增導覽列項目
--
-- 為什麼需要這支手動 SQL：DbInitializer 的兩個 seed 方法都有「表裡已經有資料就整段跳過」
-- 的守衛（NavModules.Any() / RoleModulePermissions.Any()），所以既有資料庫**永遠不會**
-- 拿到新增的列。`DbInitializer.cs` 只保證「全新環境從零建起來時是對的」，
-- 既有環境一律要另外跑這支。兩件事缺一不可（教訓見 DevLog 條目 286）。
--
-- 執行方式：對正式資料庫執行一次。可重複執行（有 IF NOT EXISTS 保護）。

BEGIN TRANSACTION;

DECLARE @ParentId INT;
DECLARE @NewModuleId INT;

-- 父層＝「青農戰情室」（模組 2）。用 Route 定位而不是寫死 Id：
-- Id 是自增代理鍵，不同環境不保證一致
SELECT @ParentId = Id FROM core.NavModules WHERE Route = N'/weather' AND ParentId IS NULL;

IF @ParentId IS NULL
BEGIN
    RAISERROR (N'找不到父層 /weather，請確認 NavModules 是否已初始化', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM core.NavModules WHERE Route = N'/weather/pesticides')
BEGIN
    INSERT INTO core.NavModules (Name, Route, Icon, IsActive, SortOrder, ParentId)
    VALUES (N'農藥查詢', N'/weather/pesticides', N'mdi-spray-bottle', 1, 6, @ParentId);

    SET @NewModuleId = SCOPE_IDENTITY();
END
ELSE
BEGIN
    SELECT @NewModuleId = Id FROM core.NavModules WHERE Route = N'/weather/pesticides';
END

-- Guest 與 Admin 各一列（比照既有慣例：所有公開模組兩個角色都給 CanView）
INSERT INTO core.RoleModulePermissions (RoleId, ModuleId, CanView)
SELECT r.Id, @NewModuleId, 1
FROM dbo.AspNetRoles r
WHERE r.Name IN (N'Guest', N'Admin')
  AND NOT EXISTS (
      SELECT 1 FROM core.RoleModulePermissions p
      WHERE p.RoleId = r.Id AND p.ModuleId = @NewModuleId
  );

COMMIT TRANSACTION;

-- 驗證：應該回一列 NavModule 與兩列權限
SELECT m.Id, m.Name, m.Route, m.Icon, m.IsActive, m.SortOrder, m.ParentId
FROM core.NavModules m WHERE m.Route = N'/weather/pesticides';

SELECT r.Name AS RoleName, p.ModuleId, p.CanView
FROM core.RoleModulePermissions p
JOIN dbo.AspNetRoles r ON r.Id = p.RoleId
JOIN core.NavModules m ON m.Id = p.ModuleId
WHERE m.Route = N'/weather/pesticides';

--SELECT @@TRANCOUNT;

--COMMIT TRANSACTION;