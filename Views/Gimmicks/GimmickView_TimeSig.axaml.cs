using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MercuryMapper.Views.Gimmicks;

public partial class GimmickView_TimeSig : UserControl
{
    public GimmickView_TimeSig(int upper = 4, int lower = 4)
    {
        InitializeComponent();

        TimeSigUpperNumberBox.Value = upper;
        TimeSigLowerNumberBox.Value = lower;
    }
}