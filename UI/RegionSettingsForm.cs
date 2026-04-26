using System;
using System.Drawing;
using System.Windows.Forms;
using AOUU.Models;

namespace AOUU.UI;

public sealed class RegionSettingsForm : Form
{
    private readonly RegionSettingsMode _mode;
    private readonly TextBox _nameTextBox;
    private readonly NumericUpDown _consecutiveFramesBox;
    private readonly NumericUpDown _readySimilarityBox;
    private readonly NumericUpDown _diffPixelBox;
    private readonly NumericUpDown _changedAreaRatioBox;

    public RegionSettingsForm(RegionSettingsMode mode, Rectangle selectedBounds)
    {
        _mode = mode;

        Text = mode == RegionSettingsMode.Skill ? "技能区域配置" : "血条区域配置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 440;
        Height = mode == RegionSettingsMode.Skill ? 280 : 310;

        var isSkill = mode == RegionSettingsMode.Skill;

        var boundsLabel = new Label
        {
            Left = 20,
            Top = 20,
            Width = 380,
            Text = $"已选区域：{selectedBounds.X},{selectedBounds.Y} {selectedBounds.Width}x{selectedBounds.Height}"
        };

        var nameLabel = new Label
        {
            Left = 20,
            Top = 58,
            Width = 120,
            Text = "区域名称"
        };

        _nameTextBox = new TextBox
        {
            Left = 160,
            Top = 54,
            Width = 220,
            Text = isSkill ? "技能区域" : "血条区域"
        };

        var consecutiveLabel = new Label
        {
            Left = 20,
            Top = 96,
            Width = 120,
            Text = "连续命中帧数"
        };

        _consecutiveFramesBox = new NumericUpDown
        {
            Left = 160,
            Top = 92,
            Width = 120,
            Minimum = 1,
            Maximum = 10,
            Value = 2
        };

        var readyLabel = new Label
        {
            Left = 20,
            Top = 134,
            Width = 120,
            Text = "就绪相似度",
            Visible = isSkill
        };

        _readySimilarityBox = new NumericUpDown
        {
            Left = 160,
            Top = 130,
            Width = 120,
            Minimum = 0.10M,
            Maximum = 1.00M,
            DecimalPlaces = 2,
            Increment = 0.01M,
            Value = 0.92M,
            Visible = isSkill
        };

        var diffPixelLabel = new Label
        {
            Left = 20,
            Top = 134,
            Width = 120,
            Text = "像素差阈值",
            Visible = !isSkill
        };

        _diffPixelBox = new NumericUpDown
        {
            Left = 160,
            Top = 130,
            Width = 120,
            Minimum = 1,
            Maximum = 255,
            Value = 30,
            Visible = !isSkill
        };

        var changedAreaLabel = new Label
        {
            Left = 20,
            Top = 172,
            Width = 120,
            Text = "变化面积阈值",
            Visible = !isSkill
        };

        _changedAreaRatioBox = new NumericUpDown
        {
            Left = 160,
            Top = 168,
            Width = 120,
            Minimum = 0.01M,
            Maximum = 1.00M,
            DecimalPlaces = 2,
            Increment = 0.01M,
            Value = 0.08M,
            Visible = !isSkill
        };

        var confirmButton = new Button
        {
            Left = 200,
            Top = isSkill ? 190 : 220,
            Width = 80,
            Text = "确定"
        };
        confirmButton.Click += ConfirmButton_Click;

        var cancelButton = new Button
        {
            Left = 290,
            Top = isSkill ? 190 : 220,
            Width = 80,
            Text = "取消"
        };
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.Add(boundsLabel);
        Controls.Add(nameLabel);
        Controls.Add(_nameTextBox);
        Controls.Add(consecutiveLabel);
        Controls.Add(_consecutiveFramesBox);
        Controls.Add(readyLabel);
        Controls.Add(_readySimilarityBox);
        Controls.Add(diffPixelLabel);
        Controls.Add(_diffPixelBox);
        Controls.Add(changedAreaLabel);
        Controls.Add(_changedAreaRatioBox);
        Controls.Add(confirmButton);
        Controls.Add(cancelButton);
    }

    public WatchRegion CreateRegion(ScreenBounds bounds, string? readyTemplateImagePath = null)
    {
        if (_mode == RegionSettingsMode.Skill)
        {
            return new SkillReadyWatchRegion
            {
                Name = _nameTextBox.Text.Trim(),
                Bounds = bounds,
                ConsecutiveFramesRequired = (int)_consecutiveFramesBox.Value,
                ReadySimilarityThreshold = (double)_readySimilarityBox.Value,
                ReadyTemplateImagePath = readyTemplateImagePath ?? string.Empty
            };
        }

        return new HealthChangeWatchRegion
        {
            Name = _nameTextBox.Text.Trim(),
            Bounds = bounds,
            ConsecutiveFramesRequired = (int)_consecutiveFramesBox.Value,
            DiffPixelThreshold = (byte)_diffPixelBox.Value,
            ChangedAreaRatioThreshold = (double)_changedAreaRatioBox.Value
        };
    }

    private void ConfirmButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            MessageBox.Show(this, "请填写区域名称。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}

public enum RegionSettingsMode
{
    Skill,
    Health
}
