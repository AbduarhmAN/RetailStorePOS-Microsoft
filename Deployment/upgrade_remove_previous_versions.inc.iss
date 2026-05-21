const
  RetailStorePOSCanonicalAppId = '{14A5B548-6B24-447E-86DA-D96F702D4D4F}';
  RetailStorePOSLegacyAppIdUptodown = '{A1B2C3D4-E5F6-7890-1234-567890ABCDEF}';
  RetailStorePOSLegacyAppIdSingleFileSmall = '{4EF6C0E7-54AD-4E8F-9F55-1989B6B27FB8}';

function TryGetUninstallValue(const RootKey: Integer; const SubKey, ValueName: string; var Value: string): Boolean;
begin
  Result := RegQueryStringValue(RootKey, SubKey, ValueName, Value) and (Trim(Value) <> '');
end;

function TryGetLegacyUninstallCommand(const AppId: string; var UninstallCommand: string; var IsQuietCommand: Boolean): Boolean;
var
  SubKey: string;
begin
  SubKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + AppId + '_is1';

  if TryGetUninstallValue(HKCU, SubKey, 'QuietUninstallString', UninstallCommand) or
     TryGetUninstallValue(HKLM, SubKey, 'QuietUninstallString', UninstallCommand) then
  begin
    IsQuietCommand := True;
    Result := True;
    Exit;
  end;

  if TryGetUninstallValue(HKCU, SubKey, 'UninstallString', UninstallCommand) or
     TryGetUninstallValue(HKLM, SubKey, 'UninstallString', UninstallCommand) then
  begin
    IsQuietCommand := False;
    Result := True;
    Exit;
  end;

  Result := False;
end;

procedure SplitCommand(const Command: string; var FileName: string; var Params: string);
var
  Parsed: string;
  SeparatorIndex: Integer;
begin
  Parsed := Trim(Command);
  FileName := '';
  Params := '';

  if Parsed = '' then
  begin
    Exit;
  end;

  if Parsed[1] = '"' then
  begin
    Delete(Parsed, 1, 1);
    SeparatorIndex := Pos('"', Parsed);
    if SeparatorIndex > 0 then
    begin
      FileName := Copy(Parsed, 1, SeparatorIndex - 1);
      Delete(Parsed, 1, SeparatorIndex);
      Params := Trim(Parsed);
    end
    else
    begin
      FileName := Parsed;
    end;
  end
  else
  begin
    SeparatorIndex := Pos(' ', Parsed);
    if SeparatorIndex > 0 then
    begin
      FileName := Copy(Parsed, 1, SeparatorIndex - 1);
      Params := Trim(Copy(Parsed, SeparatorIndex + 1, MaxInt));
    end
    else
    begin
      FileName := Parsed;
    end;
  end;
end;

function ExecuteUninstallCommand(const Command: string; IsQuietCommand: Boolean): Boolean;
var
  FileName: string;
  Params: string;
  ResultCode: Integer;
begin
  SplitCommand(Command, FileName, Params);
  if FileName = '' then
  begin
    Result := False;
    Exit;
  end;

  if not IsQuietCommand then
  begin
    Params := Trim(Params + ' /VERYSILENT /SUPPRESSMSGBOXES /NORESTART');
  end;

  Result := Exec(FileName, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function RemoveLegacyRetailStorePOSInstall(const AppId: string): Boolean;
var
  UninstallCommand: string;
  IsQuietCommand: Boolean;
begin
  Result := True;

  if not TryGetLegacyUninstallCommand(AppId, UninstallCommand, IsQuietCommand) then
  begin
    Exit;
  end;

  Log(Format('Removing legacy Retail Store POS install with AppId %s', [AppId]));
  if not ExecuteUninstallCommand(UninstallCommand, IsQuietCommand) then
  begin
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';

  if not RemoveLegacyRetailStorePOSInstall(RetailStorePOSLegacyAppIdUptodown) then
  begin
    Result := 'Unable to remove the older Retail Store POS installation automatically. Please close the old app and try again.';
    Exit;
  end;

  if not RemoveLegacyRetailStorePOSInstall(RetailStorePOSLegacyAppIdSingleFileSmall) then
  begin
    Result := 'Unable to remove the older Retail Store POS installation automatically. Please close the old app and try again.';
    Exit;
  end;
end;
