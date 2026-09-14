using System.Windows;
using System.Windows.Markup;

namespace Battlestation;

internal static class TerminalScrollBar
{
    // Restyle the renderer's existing ScrollBar, retaining its native scroll wiring.
    public static Style CreateStyle() => (Style)XamlReader.Parse("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
               TargetType="ScrollBar">
          <Setter Property="Width" Value="16"/>
          <Setter Property="Background" Value="Transparent"/>
          <Setter Property="Focusable" Value="False"/>
          <Setter Property="Template">
            <Setter.Value>
              <ControlTemplate TargetType="ScrollBar">
                <Grid Background="Transparent" Margin="0,5">
                  <Border Margin="5,0" CornerRadius="3" Background="#0DE8D9F6"/>
                  <Track x:Name="PART_Track" Orientation="Vertical" IsDirectionReversed="True">
                    <Track.Resources>
                      <Style TargetType="RepeatButton">
                        <Setter Property="Focusable" Value="False"/>
                        <Setter Property="Template">
                          <Setter.Value>
                            <ControlTemplate TargetType="RepeatButton">
                              <Border Background="Transparent"/>
                            </ControlTemplate>
                          </Setter.Value>
                        </Setter>
                      </Style>
                    </Track.Resources>
                    <Track.DecreaseRepeatButton>
                      <RepeatButton Command="ScrollBar.PageUpCommand"/>
                    </Track.DecreaseRepeatButton>
                    <Track.Thumb>
                      <Thumb Cursor="Hand">
                        <Thumb.Template>
                          <ControlTemplate TargetType="Thumb">
                            <Border x:Name="Glass" Margin="3,0" CornerRadius="5"
                                    BorderThickness="1" BorderBrush="#8CDDD0EE" Opacity="0.72">
                              <Border.Background>
                                <LinearGradientBrush StartPoint="0,0" EndPoint="1,0.3">
                                  <GradientStop Color="#B5E7DDF1" Offset="0"/>
                                  <GradientStop Color="#709F85BC" Offset="0.48"/>
                                  <GradientStop Color="#8CBDABD6" Offset="1"/>
                                </LinearGradientBrush>
                              </Border.Background>
                            </Border>
                            <ControlTemplate.Triggers>
                              <Trigger Property="IsMouseOver" Value="True">
                                <Trigger.EnterActions>
                                  <BeginStoryboard><Storyboard>
                                    <DoubleAnimation Storyboard.TargetName="Glass"
                                      Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.12"/>
                                  </Storyboard></BeginStoryboard>
                                </Trigger.EnterActions>
                                <Trigger.ExitActions>
                                  <BeginStoryboard><Storyboard>
                                    <DoubleAnimation Storyboard.TargetName="Glass"
                                      Storyboard.TargetProperty="Opacity" To="0.72" Duration="0:0:0.2"/>
                                  </Storyboard></BeginStoryboard>
                                </Trigger.ExitActions>
                              </Trigger>
                              <Trigger Property="IsDragging" Value="True">
                                <Setter TargetName="Glass" Property="BorderBrush" Value="#E6E9DDF5"/>
                              </Trigger>
                            </ControlTemplate.Triggers>
                          </ControlTemplate>
                        </Thumb.Template>
                      </Thumb>
                    </Track.Thumb>
                    <Track.IncreaseRepeatButton>
                      <RepeatButton Command="ScrollBar.PageDownCommand"/>
                    </Track.IncreaseRepeatButton>
                  </Track>
                </Grid>
              </ControlTemplate>
            </Setter.Value>
          </Setter>
        </Style>
        """);
}
