namespace TranscriptionClient
#nowarn "57"
#nowarn "40"
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI
open Avalonia.Layout
open Avalonia.Media
open Avalonia


[<AbstractClass; Sealed>]
type Views =
    static member main model dispatch =
            //root view
        DockPanel.create [                
            DockPanel.children [
                Grid.create [
                    Grid.rowDefinitions "150.,*,30."
                    Grid.columnDefinitions "*,*"
                    Grid.horizontalAlignment HorizontalAlignment.Stretch
                    Grid.children [
                        JobSubmissionView.create model dispatch
                        JobsListView.create  model dispatch
                        Border.create [
                            Grid.row 2
                            Grid.columnSpan 2
                            Border.horizontalAlignment HorizontalAlignment.Stretch
                            Border.verticalAlignment VerticalAlignment.Bottom
                            Border.margin 3
                            Border.background Brushes.DarkSlateGray
                            Border.borderThickness 1.0
                            Border.borderBrush Brushes.LightBlue                            
                            Border.child(
                                StackPanel.create [
                                    StackPanel.orientation Orientation.Horizontal
                                    StackPanel.children [
                                        Ellipse.create [
                                            Shapes.Ellipse.tip $"Service connection: {model.connectionState}"
                                            Shapes.Ellipse.fill (U.connectionColor model.connectionState)
                                            Shapes.Ellipse.width 10.
                                            Shapes.Ellipse.height 10.
                                            Shapes.Ellipse.margin (Thickness(5.,0.,5.,0.))
                                            Shapes.Ellipse.verticalAlignment VerticalAlignment.Center
                                        ]
                                        Vls.textBlock 
                                            (if model.connectionState.IsDisconnected then "" else $"Total jobs in service queue: {model.jobsInQueue}")
                                            [ 
                                                TextBlock.textAlignment TextAlignment.Center
                                                TextBlock.horizontalAlignment HorizontalAlignment.Stretch
                                                TextBlock.fontStyle FontStyle.Italic
                                            ]
                                    ]
                                ]
                            )
                        ]                            
                        GridSplitter.create [
                            Grid.column 1
                            Grid.rowSpan 3
                            GridSplitter.verticalAlignment VerticalAlignment.Center
                            GridSplitter.height 50.
                            GridSplitter.horizontalAlignment HorizontalAlignment.Left                                
                            GridSplitter.background Brushes.DarkGray                                
                        ]
                    ]
                ]
            ]
        ]
