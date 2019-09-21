﻿Imports System.Threading

Public Class Form1
    Private GUILoaded As Boolean = False
    Private Const PipeBuffer As Integer = 32768
    Private Sub InputBrowseBtn_Click(sender As Object, e As EventArgs) Handles InputBrowseBtn.Click
        Dim InputBrowser As New OpenFileDialog With {
            .Title = "Browse for a video file",
            .FileName = IO.Path.GetFileName(InputTxt.Text),
            .Filter = "All Files|*.*"
        }
        If Not String.IsNullOrEmpty(InputTxt.Text) Then InputBrowser.InitialDirectory = IO.Path.GetDirectoryName(InputTxt.Text)
        Dim OkAction As MsgBoxResult = InputBrowser.ShowDialog
        If OkAction = MsgBoxResult.Ok Then
            InputTxt.Text = InputBrowser.FileName
            OutputTxt.Text = IO.Path.ChangeExtension(InputBrowser.FileName, ".webm")
        End If
    End Sub

    Private Sub OutputBrowseBtn_Click(sender As Object, e As EventArgs) Handles OutputBrowseBtn.Click
        Dim OutputBrowser As New SaveFileDialog With {
            .Title = "Save Video File",
            .FileName = IO.Path.GetFileName(OutputTxt.Text),
            .Filter = "WebM|*.webm|Matroska|*.mkv"
        }
        If Not String.IsNullOrEmpty(OutputTxt.Text) Then OutputBrowser.InitialDirectory = IO.Path.GetDirectoryName(OutputTxt.Text)
        Dim OkAction As MsgBoxResult = OutputBrowser.ShowDialog
        If OkAction = MsgBoxResult.Ok Then
            OutputTxt.Text = OutputBrowser.FileName
        End If
    End Sub
    Private Sub CheckForLockFile()
        If Not String.IsNullOrWhiteSpace(tempLocationPath.Text) Then
            Dim videoFound As Boolean = False
            Dim CheckTempFolder As String() = {}
            If IO.Directory.Exists(tempLocationPath.Text) Then CheckTempFolder = IO.Directory.GetFiles(tempLocationPath.Text)
            If CheckTempFolder.Count > 0 Then
                If CheckTempFolder.Contains(tempLocationPath.Text + "\lock") And CheckTempFolder.Contains(tempLocationPath.Text + "\InputVideo") Then
                    videoFound = True
                End If
            End If
            If videoFound Then
                Dim result As DialogResult = MsgBox("The temporary folder contains temporary files from a previous session. Do you want to continue the previous encoding session?", MsgBoxStyle.YesNo)
                If result = DialogResult.Yes Then
                    OutputTxt.Text = My.Computer.FileSystem.ReadAllText(tempLocationPath.Text + "\lock").TrimEnd
                    ResumePreviousEncodeSession()
                Else
                    Dim result2 As DialogResult = MsgBox("Do you want to clean the folder?", MsgBoxStyle.YesNo)
                    If result2 = DialogResult.Yes Then
                        clean_temp_folder(tempLocationPath.Text)
                    End If
                End If
            End If
        End If
    End Sub
    Private Sub DisableElements()
        StartBtn.Enabled = False
        InputTxt.Enabled = False
        OutputTxt.Enabled = False
        InputBrowseBtn.Enabled = False
        OutputBrowseBtn.Enabled = False
        audioBitrate.Enabled = False
        quantizer.Enabled = False
        rows.Enabled = False
        columns.Enabled = False
        HME.Enabled = False
        HME0.Enabled = False
        HME1.Enabled = False
        HME2.Enabled = False
        AdditionalArguments.Enabled = False
        speed.Enabled = False
        tempLocationPath.Enabled = False
        BrowseTempLocation.Enabled = False
        SaveLogBtn.Enabled = False
        PauseResumeButton.Enabled = True
    End Sub
    Private Sub ResumePreviousEncodeSession()
        DisableElements()
        Dim StartTasks As New Thread(Sub() Part2())
        StartTasks.Start()
    End Sub
    Private Sub StartBtn_Click(sender As Object, e As EventArgs) Handles StartBtn.Click
        If String.IsNullOrWhiteSpace(InputTxt.Text) Then
            MsgBox("No input file has been specified. Please enter or browse for an input video file")
        ElseIf String.IsNullOrWhiteSpace(OutputTxt.Text) Then
            MsgBox("No output file has been specified. Please enter or browse for an output video file")
        ElseIf String.IsNullOrWhiteSpace(tempLocationPath.Text) Then
            MsgBox("Temporary folder has not been specified. Please enter or browse for a temporary path")
        Else
            Dim CheckTempFolder As String() = IO.Directory.GetFiles(tempLocationPath.Text)
            If CheckTempFolder.Count > 0 Then
                For Each item In CheckTempFolder
                    If item.Contains(".ivf") Or item.Contains(".y4m") Or item.Contains(".opus") Then
                        Dim result As DialogResult = MsgBox("The temporary folder contains temporary files. It is recommended that the folder is cleaned up for best results. Otherwise, you could get an incorrect AV1 file. Do you want to clean the folder?", MsgBoxStyle.YesNo)
                        If result = DialogResult.Yes Then
                            clean_temp_folder(tempLocationPath.Text)
                        End If
                        Exit For
                    End If
                Next
            End If
            DisableElements()
            If Not IO.Path.GetExtension(OutputTxt.Text) = ".webm" And Not IO.Path.GetExtension(OutputTxt.Text) = ".mkv" Then
                OutputTxt.Text = OutputTxt.Text = IO.Path.ChangeExtension(OutputTxt.Text, ".webm")
            End If
            IO.File.WriteAllText(tempLocationPath.Text + "\lock", OutputTxt.Text)
            IO.File.WriteAllText(tempLocationPath.Text + "\InputVideo", InputTxt.Text)
            Dim StartTasks As New Thread(Sub() Part1())
            StartTasks.Start()
        End If
    End Sub

    Private Sub Part1()
        Dim PieceSeconds As Long = 0
        If extract_audio(InputTxt.Text, My.Settings.AudioBitrate, tempLocationPath.Text) Then
            Part2()
        End If
    End Sub

    Private Sub Part2()
        Run_svtav1(IO.File.ReadAllText(tempLocationPath.Text + "/InputVideo"), tempLocationPath.Text + "\ivf-video.ivf")
        merge_audio_video(OutputTxt.Text, tempLocationPath.Text)
        If RemoveTempFiles.Checked Then clean_temp_folder(tempLocationPath.Text) Else IO.File.Delete(tempLocationPath.Text + "\lock")
        StartBtn.BeginInvoke(Sub()
                                 StartBtn.Enabled = True
                                 audioBitrate.Enabled = True
                                 speed.Enabled = True
                                 quantizer.Enabled = True
                                 rows.Enabled = True
                                 columns.Enabled = True
                                 HME.Enabled = True
                                 HME0.Enabled = True
                                 HME1.Enabled = True
                                 HME2.Enabled = True
                                 AdditionalArguments.Enabled = True
                                 tempLocationPath.Enabled = True
                                 BrowseTempLocation.Enabled = True
                                 OutputTxt.Enabled = True
                                 InputTxt.Enabled = True
                                 InputBrowseBtn.Enabled = True
                                 OutputBrowseBtn.Enabled = True
                                 SaveLogBtn.Enabled = True
                                 PauseResumeButton.Enabled = False
                             End Sub)
        MsgBox("Finished")
    End Sub
    Private Function Run_svtav1(Input_File As String, Output_File As String)
        UpdateLog("Getting Video Info")
        Dim InputPipe As New IO.Pipes.NamedPipeServerStream("in.y4m", IO.Pipes.PipeDirection.Out, -1, IO.Pipes.PipeTransmissionMode.Byte, IO.Pipes.PipeOptions.Asynchronous, PipeBuffer, 0)
        Dim SourceFrameCount As String = String.Empty
        Dim SourceWidth As String = String.Empty
        Dim SourceHeight As String = String.Empty
        Dim SourceFrameRate As String = String.Empty
        Dim SourceFrameNum As String = String.Empty
        Dim SourceFrameDen As String = String.Empty
        Using mediainfoProcess As New Process
            mediainfoProcess.StartInfo.FileName = "mediainfo.exe"
            mediainfoProcess.StartInfo.Arguments = """" + Input_File + """ -full"
            mediainfoProcess.StartInfo.CreateNoWindow = True
            mediainfoProcess.StartInfo.RedirectStandardOutput = True
            mediainfoProcess.StartInfo.RedirectStandardError = True
            mediainfoProcess.StartInfo.RedirectStandardInput = True
            mediainfoProcess.StartInfo.UseShellExecute = False
            mediainfoProcess.Start()
            While True
                If Not mediainfoProcess.StandardOutput.EndOfStream Then
                    Dim splitted_line As String() = mediainfoProcess.StandardOutput.ReadLine().Split(":")
                    If splitted_line(0).Contains("Frame count") And SourceFrameCount = String.Empty Then
                        SourceFrameCount = splitted_line(1).Trim()
                        UpdateLog("Video Frame Count: " + SourceFrameCount)
                    ElseIf splitted_line(0).Contains("Frame rate") And SourceFrameRate = String.Empty Then
                        If Not splitted_line(1).Contains("FPS") Then
                            SourceFrameRate = splitted_line(1).Trim()
                            UpdateLog("Frame Rate: " + SourceFrameRate)
                        End If
                    ElseIf splitted_line(0).Contains("FrameRate_Num") And SourceFrameNum = String.Empty Then
                        SourceFrameNum = splitted_line(1).Trim()
                        UpdateLog("Frame Rate Numerator: " + SourceFrameNum)
                    ElseIf splitted_line(0).Contains("FrameRate_Den") And SourceFrameDen = String.Empty Then
                        SourceFrameDen = splitted_line(1).Trim()
                        UpdateLog("Frame Rate Denominator: " + SourceFrameDen)
                    ElseIf splitted_line(0).Contains("Sampled_Width") And SourceWidth = String.Empty Then
                        SourceWidth = splitted_line(1).Trim()
                        UpdateLog("Video Width: " + SourceWidth)
                    ElseIf splitted_line(0).Contains("Sampled_Height") And SourceHeight = String.Empty Then
                        SourceHeight = splitted_line(1).Trim()
                        UpdateLog("Video Height: " + SourceHeight)
                    End If
                Else
                    Exit While
                End If
            End While
        End Using
        UpdateLog("Encoding Video")
        Using svtav1Process As New Process()
            svtav1Process.StartInfo.FileName = "SvtAv1EncApp.exe"
            Dim VideoBitrateString As String = "-enc-mode " + My.Settings.speed.ToString() + " -q " + My.Settings.quantizer.ToString() + " -tile-rows " + My.Settings.TilingRows.ToString() + " -tile-columns " + My.Settings.TilingColumns.ToString()
            If My.Settings.HME Then VideoBitrateString += " -hme 1 " Else VideoBitrateString += " -hme 0 "
            If My.Settings.HME0 Then VideoBitrateString += " -hme-l0 1 " Else VideoBitrateString += " -hme-l0 0 "
            If My.Settings.HME1 Then VideoBitrateString += " -hme-l1 1 " Else VideoBitrateString += " -hme-l1 0 "
            If My.Settings.HME2 Then VideoBitrateString += " -hme-l2 1 " Else VideoBitrateString += " -hme-l2 0 "
            If SourceFrameNum = String.Empty And SourceFrameDen = String.Empty Then
                VideoBitrateString += " -fps " + SourceFrameRate
            Else
                VideoBitrateString += " -fps-num " + SourceFrameNum + " -fps-denom " + SourceFrameDen
            End If
            svtav1Process.StartInfo.Arguments = VideoBitrateString + " " + My.Settings.AdditionalArguments + " -n " + SourceFrameCount + " -w " + SourceWidth + " -h " + SourceHeight + " -i ""\\.\pipe\in.y4m"" -b """ + Output_File + """"
            svtav1Process.StartInfo.CreateNoWindow = True
            svtav1Process.StartInfo.RedirectStandardOutput = True
            svtav1Process.StartInfo.RedirectStandardError = True
            svtav1Process.StartInfo.RedirectStandardInput = True
            svtav1Process.StartInfo.UseShellExecute = False
            AddHandler svtav1Process.OutputDataReceived, New DataReceivedEventHandler(Sub(sender, e)
                                                                                          If Not e.Data = Nothing Then
                                                                                              UpdateLog(e.Data, IO.Path.GetFileName(Input_File))
                                                                                          End If
                                                                                      End Sub)
            AddHandler svtav1Process.ErrorDataReceived, New DataReceivedEventHandler(Sub(sender, e)
                                                                                         If Not e.Data = Nothing Then
                                                                                             UpdateLog(e.Data, IO.Path.GetFileName(Input_File))
                                                                                         End If
                                                                                     End Sub)
            svtav1Process.Start()
            svtav1Process.BeginOutputReadLine()
            svtav1Process.BeginErrorReadLine()
            WriteByteAsync(InputPipe, Input_File)
            svtav1Process.WaitForExit()
            UpdateLog("Video encoding complete.")
        End Using
        Return True
    End Function
    Private Async Sub WriteByteAsync(InputPipe As IO.Pipes.NamedPipeServerStream, Input As String)
        Dim OutputPipe As New IO.Pipes.NamedPipeServerStream(IO.Path.GetFileNameWithoutExtension(Input) + "-out.yuv", IO.Pipes.PipeDirection.In, -1, IO.Pipes.PipeTransmissionMode.Byte, IO.Pipes.PipeOptions.WriteThrough, 0, PipeBuffer)
        Dim ffmpegProcessInfo As New ProcessStartInfo
        Dim ffmpegProcess As Process
        ffmpegProcessInfo.FileName = "ffmpeg.exe"
        ffmpegProcessInfo.Arguments = "-i """ + Input + """ -c:v rawvideo -pix_fmt yuv420p  ""\\.\pipe\" + IO.Path.GetFileNameWithoutExtension(Input) + "-out.yuv"" -y"
        ffmpegProcessInfo.CreateNoWindow = True
        ffmpegProcessInfo.RedirectStandardInput = True
        ffmpegProcessInfo.RedirectStandardOutput = True
        ffmpegProcessInfo.UseShellExecute = False
        ffmpegProcess = Process.Start(ffmpegProcessInfo)
        Dim lastRead As Integer
        OutputPipe.WaitForConnection()
        InputPipe.WaitForConnection()
        Dim buffer As Byte() = New Byte(PipeBuffer) {}
        Do
            lastRead = OutputPipe.Read(buffer, 0, PipeBuffer)
            Await InputPipe.WriteAsync(buffer, 0, lastRead)
            InputPipe.Flush()
        Loop While lastRead > 0
        OutputPipe.Dispose()
        ffmpegProcess.WaitForExit()
        InputPipe.Dispose()
    End Sub
    Private Function clean_temp_folder(tempFolder As String)
        For Each File As String In IO.Directory.GetFiles(tempFolder)
            If IO.Path.GetExtension(File) = ".ivf" Or IO.Path.GetFileName(File) = "opus-audio.opus" Or IO.Path.GetFileName(File) = "lock" Or IO.Path.GetFileName(File) = "InputVideo" Then
                IO.File.Delete(File)
            End If
        Next
        Return True
    End Function
    Private Function merge_audio_video(output As String, tempFolder As String)
        UpdateLog("Merging audio and video files")
        Dim ffmpegProcessInfo As New ProcessStartInfo
        Dim ffmpegProcess As Process
        ffmpegProcessInfo.FileName = "ffmpeg.exe"
        If IO.File.Exists(tempFolder + "\opus-audio.opus") Then
            ffmpegProcessInfo.Arguments = "-i """ + tempFolder + "\ivf-video.ivf"" -i """ + tempFolder + "\opus-audio.opus"" -c:v copy -c:a copy """ + output + """ -y"
        Else
            ffmpegProcessInfo.Arguments = "-i """ + tempFolder + "\ivf-video.ivf"" -c:v copy """ + output + """ -y"
        End If
        ffmpegProcessInfo.CreateNoWindow = True
        ffmpegProcessInfo.RedirectStandardOutput = False
        ffmpegProcessInfo.UseShellExecute = False
        ffmpegProcess = Process.Start(ffmpegProcessInfo)
        ffmpegProcess.WaitForExit()
        UpdateLog("Merge complete")
        Return True
    End Function

    Private Function extract_audio(input As String, bitrate As Integer, tempFolder As String)
        UpdateLog("Extracting and encoding audio")
        Dim ffmpegProcessInfo As New ProcessStartInfo
        Dim ffmpegProcess As Process
        ffmpegProcessInfo.FileName = "ffmpeg.exe"
        ffmpegProcessInfo.Arguments = "-i """ + input + """ -c:a libopus -application audio -b:a " + bitrate.ToString() + "K -af aformat=channel_layouts=""7.1|5.1|stereo"" """ + tempFolder + "\opus-audio.opus"" -y"
        ffmpegProcessInfo.CreateNoWindow = True
        ffmpegProcessInfo.RedirectStandardOutput = False
        ffmpegProcessInfo.UseShellExecute = False
        ffmpegProcess = Process.Start(ffmpegProcessInfo)
        ffmpegProcess.WaitForExit()
        UpdateLog("Audio extracted and encoded")
        Return True
    End Function
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim ignoreLocations As Boolean = False
        Dim vars As String() = Environment.GetCommandLineArgs
        If vars.Count > 1 Then
            If vars.Contains("ignore_locations") Then ignoreLocations = True
            For var As Integer = 1 To vars.Count - 1
                If Not vars(var) = "ignore_locations" Then InputTxt.Text = vars(var)
            Next
        End If
        IO.Directory.SetCurrentDirectory(IO.Path.GetDirectoryName(Process.GetCurrentProcess.MainModule.FileName))
        quantizer.Value = My.Settings.quantizer
        speed.Value = My.Settings.speed
        rows.Value = My.Settings.TilingRows
        columns.Value = My.Settings.TilingColumns
        HME.Checked = My.Settings.HME
        HME0.Checked = My.Settings.HME0
        HME1.Checked = My.Settings.HME1
        HME2.Checked = My.Settings.HME2
        AdditionalArguments.Text = My.Settings.AdditionalArguments
        audioBitrate.Value = My.Settings.AudioBitrate
        tempLocationPath.Text = My.Settings.tempFolder
        RemoveTempFiles.Checked = My.Settings.removeTempFiles
        GetFfmpegVersion()
        CheckSvtAv1()
        CheckMediaInfo()
        GUILoaded = True
        If Not String.IsNullOrWhiteSpace(tempLocationPath.Text) Then CheckForLockFile()
    End Sub

    Private Delegate Sub UpdateLogInvoker(message As String, PartName As String)
    Private Sub UpdateLog(message As String, Optional PartName As String = "")
        If ProgressLog.InvokeRequired Then
            ProgressLog.Invoke(New UpdateLogInvoker(AddressOf UpdateLog), message, PartName)
        Else
            If Not PartName = "" Then
                ProgressLog.AppendText(Date.Now().ToString() + " || " + PartName + " || " + message + vbCrLf)
            Else
                ProgressLog.AppendText(Date.Now().ToString() + " || " + message + vbCrLf)
            End If
            ProgressLog.SelectionStart = ProgressLog.Text.Length - 1
            ProgressLog.ScrollToCaret()
        End If
    End Sub
    Private Sub GetFfmpegVersion()
        Try
            Dim ffmpegProcessInfo As New ProcessStartInfo
            Dim ffmpegProcess As Process
            ffmpegProcessInfo.FileName = "ffmpeg.exe"
            ffmpegProcessInfo.CreateNoWindow = True
            ffmpegProcessInfo.RedirectStandardError = True
            ffmpegProcessInfo.UseShellExecute = False
            ffmpegProcess = Process.Start(ffmpegProcessInfo)
            ffmpegProcess.WaitForExit()
            ffmpegVersionLabel.Text = ffmpegProcess.StandardError.ReadLine()
        Catch ex As Exception
            MessageBox.Show("ffmpeg.exe was not found. Exiting...")
            Process.Start("https://moisescardona.me/downloading-ffmpeg-svt-av1-gui/")
            Me.Close()
        End Try
    End Sub

    Private Sub CheckSvtAv1()
        Try
            Dim SvtAv1ProcessInfo As New ProcessStartInfo
            Dim SvtAv1Process As Process
            SvtAv1ProcessInfo.FileName = "SvtAv1EncApp.exe"
            SvtAv1ProcessInfo.CreateNoWindow = True
            SvtAv1ProcessInfo.RedirectStandardError = True
            SvtAv1ProcessInfo.UseShellExecute = False
            SvtAv1Process = Process.Start(SvtAv1ProcessInfo)
            SvtAv1Process.WaitForExit()
        Catch ex As Exception
            MessageBox.Show("SvtAv1EncApp.exe was not found. Exiting...")
            Me.Close()
        End Try
    End Sub

    Private Sub CheckMediaInfo()
        Try
            Dim MediaInfoProcessInfo As New ProcessStartInfo
            Dim MediaInfoProcess As Process
            MediaInfoProcessInfo.FileName = "mediainfo.exe"
            MediaInfoProcessInfo.CreateNoWindow = True
            MediaInfoProcessInfo.RedirectStandardError = True
            MediaInfoProcessInfo.UseShellExecute = False
            MediaInfoProcess = Process.Start(MediaInfoProcessInfo)
            MediaInfoProcess.WaitForExit()
        Catch ex As Exception
            MessageBox.Show("SvtAv1EncApp.exe was not found. Exiting...")
            Me.Close()
        End Try
    End Sub

    Private Sub tempLocationPath_TextChanged(sender As Object, e As EventArgs) Handles tempLocationPath.TextChanged
        If GUILoaded Then
            My.Settings.tempFolder = tempLocationPath.Text
            My.Settings.Save()
        End If
    End Sub

    Private Sub quantizer_ValueChanged(sender As Object, e As EventArgs) Handles quantizer.ValueChanged
        If GUILoaded Then
            My.Settings.quantizer = quantizer.Value
            My.Settings.Save()
        End If
    End Sub

    Private Sub speed_ValueChanged(sender As Object, e As EventArgs) Handles speed.ValueChanged
        If GUILoaded Then
            My.Settings.speed = speed.Value
            My.Settings.Save()
        End If
    End Sub

    Private Sub audioBitrate_ValueChanged(sender As Object, e As EventArgs) Handles audioBitrate.ValueChanged
        If GUILoaded Then
            My.Settings.AudioBitrate = audioBitrate.Value
            My.Settings.Save()
        End If
    End Sub

    Private Sub BrowseTempLocation_Click(sender As Object, e As EventArgs) Handles BrowseTempLocation.Click
        Dim TempFolderBrowser As New FolderBrowserDialog With {
           .ShowNewFolderButton = False
       }
        Dim OkAction As MsgBoxResult = TempFolderBrowser.ShowDialog
        If OkAction = MsgBoxResult.Ok Then
            tempLocationPath.Text = TempFolderBrowser.SelectedPath
        End If
    End Sub


    Private Sub RemoveTempFiles_CheckedChanged(sender As Object, e As EventArgs) Handles RemoveTempFiles.CheckedChanged
        If GUILoaded Then
            My.Settings.removeTempFiles = RemoveTempFiles.Checked
            My.Settings.Save()
        End If
    End Sub


    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        While True
            Try
                For Each SvtAV1EncApp_proc In Process.GetProcessesByName("SvtAV1EncApp")
                    SvtAV1EncApp_proc.Kill()
                Next
                For Each ffmpeg_proc In Process.GetProcessesByName("ffmpeg")
                    ffmpeg_proc.Kill()
                Next
            Catch
            End Try
            Dim svt_av1_Processes As Array = Process.GetProcessesByName("SvtAV1EncApp")
            If svt_av1_Processes.Length = 0 Then
                Exit While
            End If
            Dim ffmpeg_Processes As Array = Process.GetProcessesByName("ffmpeg")
            If ffmpeg_Processes.Length = 0 Then
                Exit While
            End If
        End While
        For Each svtav1_gui_proc In Process.GetProcessesByName("svtav1_gui")
            If svtav1_gui_proc.Id = Process.GetCurrentProcess().Id Then svtav1_gui_proc.Kill()
        Next
    End Sub


    Private Sub ClearLogBtn_Click(sender As Object, e As EventArgs) Handles ClearLogBtn.Click
        ProgressLog.Clear()
    End Sub

    Private Sub SaveLogBtn_Click(sender As Object, e As EventArgs) Handles SaveLogBtn.Click
        Dim saveDialog As New SaveFileDialog With {
            .Filter = "Log File|*.log",
            .Title = "Browse to save the log file"}
        Dim dialogResult As DialogResult = saveDialog.ShowDialog()
        If DialogResult.OK Then
            My.Computer.FileSystem.WriteAllText(saveDialog.FileName, ProgressLog.Text, False)
        End If
    End Sub

    Private Sub Form1_DragEnter(sender As Object, e As DragEventArgs) Handles MyBase.DragEnter
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effect = DragDropEffects.Copy
        End If
    End Sub
    Private Sub Form1_DragDrop(sender As Object, e As DragEventArgs) Handles MyBase.DragDrop
        Dim Filename As String = CType(e.Data.GetData(DataFormats.FileDrop), String())(0)
        InputTxt.Text = Filename
        OutputTxt.Text = IO.Path.ChangeExtension(Filename, ".webm")
    End Sub

    Private Sub PauseResumeButton_Click(sender As Object, e As EventArgs) Handles PauseResumeButton.Click
        If PauseResumeButton.Text = "Pause" Then
            UpdateLog("Pausing encode")
            Try
                For Each SvtAV1Enc_proc In Process.GetProcessesByName("SvtAV1EncApp")
                    SuspendResumeProcess.SuspendProcess(SvtAV1Enc_proc.Id)
                Next
            Catch
            End Try
            UpdateLog("Encode paused (Some progress may still be reported)")
            PauseResumeButton.Text = "Resume"
        Else
            UpdateLog("Resuming encode")
            Try
                For Each SvtAV1Enc_proc In Process.GetProcessesByName("SvtAV1EncApp")
                    SuspendResumeProcess.ResumeProcess(SvtAV1Enc_proc.Id)
                Next
            Catch
            End Try
            UpdateLog("Encode resumed")
            PauseResumeButton.Text = "Pause"
        End If
    End Sub

    Private Sub rows_ValueChanged(sender As Object, e As EventArgs) Handles rows.ValueChanged
        If GUILoaded Then
            My.Settings.TilingRows = rows.Value
            My.Settings.Save()
        End If
    End Sub

    Private Sub Columns_ValueChanged(sender As Object, e As EventArgs) Handles columns.ValueChanged
        If GUILoaded Then
            My.Settings.TilingColumns = columns.Value
            My.Settings.Save()
        End If
    End Sub

    Private Sub HME_CheckedChanged(sender As Object, e As EventArgs) Handles HME.CheckedChanged
        If GUILoaded Then
            My.Settings.HME = HME.Checked
            My.Settings.Save()
        End If
    End Sub

    Private Sub HME0_CheckedChanged(sender As Object, e As EventArgs) Handles HME0.CheckedChanged
        If GUILoaded Then
            My.Settings.HME0 = HME0.Checked
            My.Settings.Save()
        End If
    End Sub

    Private Sub HME1_CheckedChanged(sender As Object, e As EventArgs) Handles HME1.CheckedChanged
        If GUILoaded Then
            My.Settings.HME1 = HME1.Checked
            My.Settings.Save()
        End If
    End Sub

    Private Sub HME2_CheckedChanged(sender As Object, e As EventArgs) Handles HME2.CheckedChanged
        If GUILoaded Then
            My.Settings.HME2 = HME2.Checked
            My.Settings.Save()
        End If
    End Sub

    Private Sub AdditionalArguments_TextChanged(sender As Object, e As EventArgs) Handles AdditionalArguments.TextChanged
        If GUILoaded Then
            My.Settings.AdditionalArguments = AdditionalArguments.Text
            My.Settings.Save()
        End If
    End Sub

    Private Sub InputTxt_TextChanged(sender As Object, e As EventArgs) Handles InputTxt.TextChanged
        OutputTxt.Text = IO.Path.ChangeExtension(InputTxt.Text, ".webm")
    End Sub
End Class
