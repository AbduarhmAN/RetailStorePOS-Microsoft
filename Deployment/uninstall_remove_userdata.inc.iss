var
  RemoveAllDataRequested: Boolean;

function ShowRemoveDataOptionsForm: Boolean;
var
  OptionsForm: TSetupForm;
  HeaderLabel: TNewStaticText;
  BodyLabel: TNewStaticText;
  RemoveAllDataCheckBox: TNewCheckBox;
  NextButton: TNewButton;
  CancelButton: TNewButton;
begin
  Result := False;

  OptionsForm := CreateCustomForm(ScaleX(420), ScaleY(210), False, True);
  try
    OptionsForm.Caption := 'Nexill Retail Store POS Uninstall';
    OptionsForm.BorderStyle := bsDialog;
    OptionsForm.Position := poScreenCenter;

    HeaderLabel := TNewStaticText.Create(OptionsForm);
    HeaderLabel.Parent := OptionsForm;
    HeaderLabel.Left := ScaleX(16);
    HeaderLabel.Top := ScaleY(16);
    HeaderLabel.Width := OptionsForm.ClientWidth - ScaleX(32);
    HeaderLabel.Height := ScaleY(22);
    HeaderLabel.AutoSize := False;
    HeaderLabel.Font.Style := [fsBold];
    HeaderLabel.Caption := 'Choose how to uninstall Nexill Retail Store POS';

    BodyLabel := TNewStaticText.Create(OptionsForm);
    BodyLabel.Parent := OptionsForm;
    BodyLabel.Left := HeaderLabel.Left;
    BodyLabel.Top := HeaderLabel.Top + HeaderLabel.Height + ScaleY(8);
    BodyLabel.Width := HeaderLabel.Width;
    BodyLabel.Height := ScaleY(64);
    BodyLabel.AutoSize := False;
    BodyLabel.WordWrap := True;
    BodyLabel.Caption :=
      'Click Next to remove the application.' + #13#10 + #13#10 +
      'If you also want to remove the local database, receipts, cached images, and other user data, select the checkbox below before continuing.';

    RemoveAllDataCheckBox := TNewCheckBox.Create(OptionsForm);
    RemoveAllDataCheckBox.Parent := OptionsForm;
    RemoveAllDataCheckBox.Left := HeaderLabel.Left;
    RemoveAllDataCheckBox.Top := BodyLabel.Top + BodyLabel.Height + ScaleY(4);
    RemoveAllDataCheckBox.Width := HeaderLabel.Width;
    RemoveAllDataCheckBox.Caption := 'Remove database and all local user data';
    RemoveAllDataCheckBox.Checked := False;

    NextButton := TNewButton.Create(OptionsForm);
    NextButton.Parent := OptionsForm;
    NextButton.Width := ScaleX(88);
    NextButton.Height := ScaleY(26);
    NextButton.Left := OptionsForm.ClientWidth - ScaleX(16 + 88 + 88 + 8);
    NextButton.Top := OptionsForm.ClientHeight - ScaleY(16 + 26);
    NextButton.Anchors := [akRight, akBottom];
    NextButton.Caption := 'Next >';
    NextButton.ModalResult := mrOk;
    NextButton.Default := True;

    CancelButton := TNewButton.Create(OptionsForm);
    CancelButton.Parent := OptionsForm;
    CancelButton.Width := ScaleX(88);
    CancelButton.Height := ScaleY(26);
    CancelButton.Left := OptionsForm.ClientWidth - ScaleX(16 + 88);
    CancelButton.Top := NextButton.Top;
    CancelButton.Anchors := [akRight, akBottom];
    CancelButton.Caption := 'Cancel';
    CancelButton.ModalResult := mrCancel;
    CancelButton.Cancel := True;

    OptionsForm.ActiveControl := NextButton;
    if OptionsForm.ShowModal() = mrOk then
    begin
      RemoveAllDataRequested := RemoveAllDataCheckBox.Checked;
      Result := True;
    end;
  finally
    OptionsForm.Free;
  end;
end;

function InitializeUninstall(): Boolean;
begin
  RemoveAllDataRequested := False;

  if UninstallSilent then
  begin
    Result := True;
    Exit;
  end;

  Result := ShowRemoveDataOptionsForm();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  CurrentDataRoot: String;
  LegacyDataRoot: String;
begin
  if (CurUninstallStep = usPostUninstall) and RemoveAllDataRequested then
  begin
    CurrentDataRoot := ExpandConstant('{localappdata}\RetailStorePOS');
    LegacyDataRoot := ExpandConstant('{localappdata}\RetailStorePos');

    if DirExists(CurrentDataRoot) and not DelTree(CurrentDataRoot, True, True, True) then
    begin
      Log(Format('Failed to remove current app data root: %s', [CurrentDataRoot]));
    end;

    if DirExists(LegacyDataRoot) and not DelTree(LegacyDataRoot, True, True, True) then
    begin
      Log(Format('Failed to remove legacy app data root: %s', [LegacyDataRoot]));
    end;
  end;
end;
