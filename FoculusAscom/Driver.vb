'tabs=4
' --------------------------------------------------------------------------------
' TODO fill in this information for your driver, then remove this line!
'
' ASCOM Camera driver for Foculus
'
' Description:	Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam 
'				nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam 
'				erat, sed diam voluptua. At vero eos et accusam et justo duo 
'				dolores et ea rebum. Stet clita kasd gubergren, no sea takimata 
'				sanctus est Lorem ipsum dolor sit amet.
'
' Implements:	ASCOM Camera interface version: 1.0
' Author:		(XXX) Your N. Here <your@email.here>
'
' Edit Log:
'
' Date			Who	Vers	Description
' -----------	---	-----	-------------------------------------------------------
' dd-mmm-yyyy	XXX	1.0.0	Initial edit, from Camera template
' ---------------------------------------------------------------------------------
'
'
' Your driver's ID is ASCOM.Foculus.Camera
'
' The Guid attribute sets the CLSID for ASCOM.DeviceName.Camera
' The ClassInterface/None addribute prevents an empty interface called
' _Camera from being created and used as the [default] interface
'

' This definition is used to select code that's only applicable for one device type
#Const Device = "Camera"

Imports ASCOM
Imports ASCOM.Astrometry
Imports ASCOM.Astrometry.AstroUtils
Imports ASCOM.DeviceInterface
Imports ASCOM.Utilities

Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Runtime.InteropServices
Imports System.Text

<Guid("e82b244f-6c67-4c58-bdde-6e45c16046d2")>
<ClassInterface(ClassInterfaceType.None)>
Public Class Camera

    ' The Guid attribute sets the CLSID for ASCOM.Foculus.Camera
    ' The ClassInterface/None addribute prevents an empty interface called
    ' _Foculus from being created and used as the [default] interface

    ' TODO Replace the not implemented exceptions with code to implement the function or
    ' throw the appropriate ASCOM exception.
    '
    Implements ICameraV2

    '
    ' Driver ID and descriptive string that shows in the Chooser
    '
    Friend Shared driverID As String = "ASCOM.Foculus.Camera"
    Private Shared driverDescription As String = "Foculus Camera"

    Friend Shared comPortProfileName As String = "COM Port" 'Constants used for Profile persistence
    Friend Shared traceStateProfileName As String = "Trace Level"
    Friend Shared comPortDefault As String = "COM1"
    Friend Shared traceStateDefault As String = "False"

    Friend Shared comPort As String ' Variables to hold the currrent device configuration
    Friend Shared traceState As Boolean
    Private m_exposureTime As Long
    Private connectedState As Boolean ' Private variable to hold the connected state
    Private utilities As Util ' Private variable to hold an ASCOM Utilities object
    Private astroUtilities As AstroUtils ' Private variable to hold an AstroUtils object to provide the Range method
    Private TL As TraceLogger ' Private variable to hold the trace logger object (creates a diagnostic log file with information that you specify)
    Private capturing As Boolean = False
    Public myCam As Object
    Public WithEvents FG As FGControlLib.FGControlCtrl
    '
    ' Constructor - Must be public for COM registration!
    '
    Public Sub New()

        ReadProfile() ' Read device configuration from the ASCOM Profile store
        TL = New TraceLogger("", "Foculus")
        TL.Enabled = traceState
        TL.LogMessage("Camera", "Starting initialisation")

        connectedState = False ' Initialise connected to false
        utilities = New Util() ' Initialise util object
        astroUtilities = New AstroUtils 'Initialise new astro utiliites object

        'TODO: Implement your additional construction here
        FG = New FGControlLib.FGControlCtrl
        Dim cams As String()
        'cams = FG.GetCameraList()
        FG.Camera = 0
        FG.PixelFormat = 11 ' bin 2
        FG.Flip = 1
        FG.BytePerPacket = 1000
        ' FG.bin
        'FG.AcquisitionMode = ""
        'FG.SetExposureTimeString("75ms")
        Me.FG.SetGain("", 500)
        ' Me.FG.SetExposureTimeString("50ms")
        ' FG.Binning = 1
        FG.KneeLUTEnable = True
        FG.SetLUTKneePoint(0, 525, 1290)
        FG.SetLUTKneePoint(1, 1290, 1980)
        FG.SetLUTKneePoint(2, 1860, 2265)
        FG.SetLUTKneePoint(3, 2475, 2715)
        FG.ExposureTimeAuto = "Off"
        FG.AcquisitionMode = "Continuous"
        ccdHeight = FG.SizeY
        ccdWidth = FG.SizeX
        FG.SnowNoiseRemove = 1
        '  FG.SnowNoiseRemoveThreshold = 100


        TL.LogMessage("Camera", "Completed initialisation")
    End Sub

    '
    ' PUBLIC COM INTERFACE ICameraV2 IMPLEMENTATION
    '

#Region "Common properties and methods"
    ''' <summary>
    ''' Displays the Setup Dialog form.
    ''' If the user clicks the OK button to dismiss the form, then
    ''' the new settings are saved, otherwise the old values are reloaded.
    ''' THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
    ''' </summary>
    Public Sub SetupDialog() Implements ICameraV2.SetupDialog
        ' consider only showing the setup dialog if not connected
        ' or call a different dialog if connected
        If IsConnected Then
            System.Windows.Forms.MessageBox.Show("Already connected, just press OK")
        End If

        Using F As SetupDialogForm = New SetupDialogForm()
            Dim result As System.Windows.Forms.DialogResult = F.ShowDialog()
            If result = DialogResult.OK Then
                WriteProfile() ' Persist device configuration values to the ASCOM Profile store
            End If
        End Using
    End Sub

    Public ReadOnly Property SupportedActions() As ArrayList Implements ICameraV2.SupportedActions
        Get
            TL.LogMessage("SupportedActions Get", "Returning empty arraylist")
            Return New ArrayList()
        End Get
    End Property

    Public Function Action(ByVal ActionName As String, ByVal ActionParameters As String) As String Implements ICameraV2.Action
        Throw New ActionNotImplementedException("Action " & ActionName & " is not supported by this driver")
    End Function

    Public Sub CommandBlind(ByVal Command As String, Optional ByVal Raw As Boolean = False) Implements ICameraV2.CommandBlind
        CheckConnected("CommandBlind")
        ' Call CommandString and return as soon as it finishes
        Me.CommandString(Command, Raw)
        ' or
        Throw New MethodNotImplementedException("CommandBlind")
    End Sub

    Public Function CommandBool(ByVal Command As String, Optional ByVal Raw As Boolean = False) As Boolean _
        Implements ICameraV2.CommandBool
        CheckConnected("CommandBool")
        Dim ret As String = CommandString(Command, Raw)
        ' TODO decode the return string and return true or false
        ' or
        Throw New MethodNotImplementedException("CommandBool")
    End Function

    Public Function CommandString(ByVal Command As String, Optional ByVal Raw As Boolean = False) As String _
        Implements ICameraV2.CommandString
        CheckConnected("CommandString")
        ' it's a good idea to put all the low level communication with the device here,
        ' then all communication calls this function
        ' you need something to ensure that only one command is in progress at a time
        Throw New MethodNotImplementedException("CommandString")
    End Function

    Public Property Connected() As Boolean Implements ICameraV2.Connected
        Get
            TL.LogMessage("Connected Get", IsConnected.ToString())
            Return IsConnected
        End Get
        Set(value As Boolean)
            TL.LogMessage("Connected Set", value.ToString())
            If value = IsConnected Then
                Return
            End If

            If value Then
                connectedState = True
                TL.LogMessage("Connected Set", "Connecting to port " + comPort)
                ' TODO connect to the device
            Else
                connectedState = False
                TL.LogMessage("Connected Set", "Disconnecting from port " + comPort)
                ' TODO disconnect from the device
            End If
        End Set
    End Property

    Public ReadOnly Property Description As String Implements ICameraV2.Description
        Get
            ' this pattern seems to be needed to allow a public property to return a private field
            Dim d As String = driverDescription
            TL.LogMessage("Description Get", d)
            Return d
        End Get
    End Property

    Public ReadOnly Property DriverInfo As String Implements ICameraV2.DriverInfo
        Get
            Dim m_version As Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
            ' TODO customise this driver description
            Dim s_driverInfo As String = "Information about the driver itself. Version: " + m_version.Major.ToString() + "." + m_version.Minor.ToString()
            TL.LogMessage("DriverInfo Get", s_driverInfo)
            Return s_driverInfo
        End Get
    End Property

    Public ReadOnly Property DriverVersion() As String Implements ICameraV2.DriverVersion
        Get
            ' Get our own assembly and report its version number
            TL.LogMessage("DriverVersion Get", Reflection.Assembly.GetExecutingAssembly.GetName.Version.ToString(2))
            Return Reflection.Assembly.GetExecutingAssembly.GetName.Version.ToString(2)
        End Get
    End Property

    Public ReadOnly Property InterfaceVersion() As Short Implements ICameraV2.InterfaceVersion
        Get
            TL.LogMessage("InterfaceVersion Get", "2")
            Return 2
        End Get
    End Property

    Public ReadOnly Property Name As String Implements ICameraV2.Name
        Get
            Dim s_name As String = "Short driver name - please customise"
            TL.LogMessage("Name Get", s_name)
            Return s_name
        End Get
    End Property

    Public Sub Dispose() Implements ICameraV2.Dispose
        ' Clean up the tracelogger and util objects
        TL.Enabled = False
        TL.Dispose()
        TL = Nothing
        utilities.Dispose()
        utilities = Nothing
        astroUtilities.Dispose()
        astroUtilities = Nothing
    End Sub

#End Region

#Region "ICamera Implementation"

    Private ccdWidth As Integer = 1388 ' Constants to define the ccd pixel dimenstions
    Private ccdHeight As Integer = 1040
    Private pixelSize As Double = 6.45 ' Constant for the pixel physical dimension

    Private cameraNumX As Integer = ccdWidth ' Initialise variables to hold values required for functionality tested by Conform
    Private cameraNumY As Integer = ccdHeight
    Private cameraStartX As Integer = 0
    Private cameraStartY As Integer = 0
    Private exposureStart As DateTime = DateTime.MinValue
    Private cameraLastExposureDuration As Double = 0.0
    Private cameraImageReady As Boolean = False
    Private cameraImageArray As Integer(,)
    Private cameraImageArrayVariant As Object(,)

    Public Sub AbortExposure() Implements ICameraV2.AbortExposure
        TL.LogMessage("AbortExposure", "Not implemented")
        Throw New MethodNotImplementedException("AbortExposure")
    End Sub

    Public ReadOnly Property BayerOffsetX() As Short Implements ICameraV2.BayerOffsetX
        Get
            TL.LogMessage("BayerOffsetX Get", "Not implemented")
            Throw New PropertyNotImplementedException("BayerOffsetX", False)
        End Get
    End Property

    Public ReadOnly Property BayerOffsetY() As Short Implements ICameraV2.BayerOffsetY
        Get
            TL.LogMessage("BayerOffsetY Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("BayerOffsetY", False)
        End Get
    End Property

    Public Property BinX() As Short Implements ICameraV2.BinX
        Get
            TL.LogMessage("BinX Get", "2")
            If FG.PixelFormat = 11 Then
                Return 2
            End If
            If FG.PixelFormat = 9 Then
                Return 1

            End If
        End Get
        Set(value As Short)
            TL.LogMessage("BinX Set", value.ToString())
            If value = 2 Then
                FG.PixelFormat = 11
                FG.BytePerPacket = 1000
            End If
            If value = 1 Then
                FG.PixelFormat = 9
                FG.BytePerPacket = 1000

            End If
            'If (Not (value = 1)) Then
            '    TL.LogMessage("BinX Set", "Value out of range, throwing InvalidValueException")
            '    Throw New ASCOM.InvalidValueException("BinX", value.ToString(), "2") ' Only 1 is valid in this simple template
            'End If
        End Set
    End Property

    Public Property BinY() As Short Implements ICameraV2.BinY
        Get
            Return BinX()
        End Get
        Set(value As Short)
            TL.LogMessage("BinY Set", value.ToString())
            'If (Not (value = 1)) Then
            '    TL.LogMessage("BinX Set", "Value out of range, throwing InvalidValueException")
            '    Throw New ASCOM.InvalidValueException("BinY", value.ToString(), "2") ' Only 1 is valid in this simple template
            'End If
        End Set
    End Property

    Public ReadOnly Property CCDTemperature() As Double Implements ICameraV2.CCDTemperature
        Get
            TL.LogMessage("CCDTemperature Get", "Not implemented")
            Return 0
        End Get
    End Property

    Public ReadOnly Property CameraState() As CameraStates Implements ICameraV2.CameraState
        Get
            TL.LogMessage("CameraState Get", CameraStates.cameraIdle.ToString())
            Return CameraStates.cameraIdle
        End Get
    End Property

    Public ReadOnly Property CameraXSize() As Integer Implements ICameraV2.CameraXSize
        Get
            TL.LogMessage("CameraXSize Get", ccdWidth.ToString())
            If FG.PixelFormat = 11 Then '2x2
                ' Return FG.SizeX * 2
                Return 688 * 2
            End If
            If FG.PixelFormat = 9 Then '1xbinned
                ' Return FG.SizeX - 1
                Return 1388
            End If
        End Get
    End Property

    Public ReadOnly Property CameraYSize() As Integer Implements ICameraV2.CameraYSize
        Get
            TL.LogMessage("CameraYSize Get", ccdHeight.ToString())
            If FG.PixelFormat = 11 Then 'binned
                Return 516 * 2
            End If
            If FG.PixelFormat = 9 Then 'binned
                Return 1040
            End If
        End Get
    End Property

    Public ReadOnly Property CanAbortExposure() As Boolean Implements ICameraV2.CanAbortExposure
        Get
            TL.LogMessage("CanAbortExposure Get", False.ToString())
            Return False
        End Get
    End Property

    Public ReadOnly Property CanAsymmetricBin() As Boolean Implements ICameraV2.CanAsymmetricBin
        Get
            TL.LogMessage("CanAsymmetricBin Get", False.ToString())
            Return True
        End Get
    End Property

    Public ReadOnly Property CanFastReadout() As Boolean Implements ICameraV2.CanFastReadout
        Get
            TL.LogMessage("CanFastReadout Get", False.ToString())
            Return False
        End Get
    End Property

    Public ReadOnly Property CanGetCoolerPower() As Boolean Implements ICameraV2.CanGetCoolerPower
        Get
            TL.LogMessage("CanGetCoolerPower Get", False.ToString())
            Return False
        End Get
    End Property

    Public ReadOnly Property CanPulseGuide() As Boolean Implements ICameraV2.CanPulseGuide
        Get
            TL.LogMessage("CanPulseGuide Get", False.ToString())
            Return False
        End Get
    End Property

    Public ReadOnly Property CanSetCCDTemperature() As Boolean Implements ICameraV2.CanSetCCDTemperature
        Get
            TL.LogMessage("CanSetCCDTemperature Get", False.ToString())
            Return False
        End Get
    End Property

    Public ReadOnly Property CanStopExposure() As Boolean Implements ICameraV2.CanStopExposure
        Get
            TL.LogMessage("CanStopExposure Get", False.ToString())
            Return False
        End Get
    End Property

    Public Property CoolerOn() As Boolean Implements ICameraV2.CoolerOn
        Get
            TL.LogMessage("CoolerOn Get", "Not implemented")
            'Throw New ASCOM.PropertyNotImplementedException("CoolerOn", False)
            Return False
        End Get
        Set(value As Boolean)
            TL.LogMessage("CoolerOn Set", "Not implemented")
            'Throw New ASCOM.PropertyNotImplementedException("CoolerOn", True)
            value = value
        End Set
    End Property

    Public ReadOnly Property CoolerPower() As Double Implements ICameraV2.CoolerPower
        Get
            TL.LogMessage("AbortExposure Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("CoolerPower", False)
        End Get
    End Property

    Public ReadOnly Property ElectronsPerADU() As Double Implements ICameraV2.ElectronsPerADU
        Get
            TL.LogMessage("ElectronsPerADU Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ElectronsPerADU", False)
        End Get
    End Property

    Public ReadOnly Property ExposureMax() As Double Implements ICameraV2.ExposureMax
        Get
            TL.LogMessage("ExposureMax Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ExposureMax", False)
        End Get
    End Property

    Public ReadOnly Property ExposureMin() As Double Implements ICameraV2.ExposureMin
        Get
            TL.LogMessage("ExposureMin Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ExposureMin", False)
        End Get
    End Property

    Public ReadOnly Property ExposureResolution() As Double Implements ICameraV2.ExposureResolution
        Get
            TL.LogMessage("ExposureResolution Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ExposureResolution", False)
        End Get
    End Property
    Public Property ExposureTime() As Long
        Get
            Return m_exposureTime
        End Get
        Set(value As Long)
            Try
                m_exposureTime = value 'stored as seconds
                FG.SetExposureTimeString(CStr(value) & "s")
                'v.m_Camera.Features("ExposureTimeAbs").FloatValue = Convert.ToDouble(m_exposureTime * 1000000) 'uses microseconds
            Catch ex As Exception
                MsgBox(ex.Message)
            End Try

        End Set
    End Property
    Public Property FastReadout() As Boolean Implements ICameraV2.FastReadout
        Get
            TL.LogMessage("FastReadout Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("FastReadout", False)
        End Get
        Set(value As Boolean)
            TL.LogMessage("FastReadout Set", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("FastReadout", True)
        End Set
    End Property

    Public ReadOnly Property FullWellCapacity() As Double Implements ICameraV2.FullWellCapacity
        Get
            TL.LogMessage("FullWellCapacity Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("FullWellCapacity", False)
        End Get
    End Property

    Public Property Gain() As Short Implements ICameraV2.Gain
        Get
            TL.LogMessage("Gain Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("Gain", False)
        End Get
        Set(value As Short)
            TL.LogMessage("Gain Set", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("Gain", True)
        End Set
    End Property

    Public ReadOnly Property GainMax() As Short Implements ICameraV2.GainMax
        Get
            TL.LogMessage("GainMax Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("GainMax", False)
        End Get
    End Property

    Public ReadOnly Property GainMin() As Short Implements ICameraV2.GainMin
        Get
            TL.LogMessage("GainMin Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("GainMin", False)
        End Get
    End Property

    Public ReadOnly Property Gains() As ArrayList Implements ICameraV2.Gains
        Get
            TL.LogMessage("Gains Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("Gains", False)
        End Get
    End Property

    Public ReadOnly Property HasShutter() As Boolean Implements ICameraV2.HasShutter
        Get
            TL.LogMessage("HasShutter Get", False.ToString())
            Return False
        End Get
    End Property

    Public ReadOnly Property HeatSinkTemperature() As Double Implements ICameraV2.HeatSinkTemperature
        Get
            TL.LogMessage("HeatSinkTemperature Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("HeatSinkTemperature", False)
        End Get
    End Property

    Public ReadOnly Property ImageArray() As Object Implements ICameraV2.ImageArray
        Get
            If (Not cameraImageReady) Then
                TL.LogMessage("ImageArray Get", "Throwing InvalidOperationException because of a call to ImageArray before the first image has been taken!")
                Throw New ASCOM.InvalidOperationException("Call to ImageArray before the first image has been taken!")
            End If
            Debug.Print("pulling imageArray")
            '  ReDim cameraImageArray(cameraNumX - 1, cameraNumY - 1)
            Return cameraImageArray
        End Get
    End Property

    Public ReadOnly Property ImageArrayVariant() As Object Implements ICameraV2.ImageArrayVariant
        Get
            If (Not cameraImageReady) Then
                TL.LogMessage("ImageArrayVariant Get", "Throwing InvalidOperationException because of a call to ImageArrayVariant before the first image has been taken!")
                Throw New ASCOM.InvalidOperationException("Call to ImageArrayVariant before the first image has been taken!")
            End If

            ReDim cameraImageArrayVariant(cameraNumX - 1, cameraNumY - 1)
            For i As Integer = 0 To cameraImageArray.GetLength(1) - 1
                For j As Integer = 0 To cameraImageArray.GetLength(0) - 1
                    cameraImageArrayVariant(j, i) = cameraImageArray(j, i)
                Next
            Next

            Return cameraImageArrayVariant
        End Get
    End Property

    Public ReadOnly Property ImageReady() As Boolean Implements ICameraV2.ImageReady
        Get
            TL.LogMessage("ImageReady Get", cameraImageReady.ToString())
            Return cameraImageReady
        End Get
    End Property

    Public ReadOnly Property IsPulseGuiding() As Boolean Implements ICameraV2.IsPulseGuiding
        Get
            TL.LogMessage("IsPulseGuiding Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("IsPulseGuiding", False)
        End Get
    End Property

    Public ReadOnly Property LastExposureDuration() As Double Implements ICameraV2.LastExposureDuration
        Get
            If (Not cameraImageReady) Then
                TL.LogMessage("LastExposureDuration Get", "Throwing InvalidOperationException because of a call to LastExposureDuration before the first image has been taken!")
                Throw New ASCOM.InvalidOperationException("Call to LastExposureDuration before the first image has been taken!")
            End If
            TL.LogMessage("LastExposureDuration Get", cameraLastExposureDuration.ToString())
            Return cameraLastExposureDuration
        End Get
    End Property

    Public ReadOnly Property LastExposureStartTime() As String Implements ICameraV2.LastExposureStartTime
        Get
            If (Not cameraImageReady) Then
                TL.LogMessage("LastExposureStartTime Get", "Throwing InvalidOperationException because of a call to LastExposureStartTime before the first image has been taken!")
                Throw New ASCOM.InvalidOperationException("Call to LastExposureStartTime before the first image has been taken!")
            End If
            Dim exposureStartString As String = exposureStart.ToString("yyyy-MM-ddTHH:mm:ss")
            TL.LogMessage("LastExposureStartTime Get", exposureStartString.ToString())
            Return exposureStartString
        End Get
    End Property

    Public ReadOnly Property MaxADU() As Integer Implements ICameraV2.MaxADU
        Get
            TL.LogMessage("MaxADU Get", "20000")
            Return 20000
        End Get
    End Property

    Public ReadOnly Property MaxBinX() As Short Implements ICameraV2.MaxBinX
        Get
            TL.LogMessage("MaxBinX Get", "1")
            Return 2
        End Get
    End Property

    Public ReadOnly Property MaxBinY() As Short Implements ICameraV2.MaxBinY
        Get
            TL.LogMessage("MaxBinY Get", "1")
            Return 2
        End Get
    End Property

    Public Property NumX() As Integer Implements ICameraV2.NumX
        'what is numx?
        Get
            TL.LogMessage("NumX Get", cameraNumX.ToString())
            Return cameraNumX
        End Get
        Set(value As Integer)
            cameraNumX = value
            TL.LogMessage("NumX set", value.ToString())
        End Set
    End Property

    Public Property NumY() As Integer Implements ICameraV2.NumY
        Get
            TL.LogMessage("NumY Get", cameraNumY.ToString())
            Return cameraNumY
        End Get
        Set(value As Integer)
            cameraNumY = value
            TL.LogMessage("NumY set", value.ToString())
        End Set
    End Property

    Public ReadOnly Property PercentCompleted() As Short Implements ICameraV2.PercentCompleted
        Get
            TL.LogMessage("PercentCompleted Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("PercentCompleted", False)
        End Get
    End Property

    Public ReadOnly Property PixelSizeX() As Double Implements ICameraV2.PixelSizeX
        Get
            TL.LogMessage("PixelSizeX Get", pixelSize.ToString())
            Return pixelSize
        End Get
    End Property

    Public ReadOnly Property PixelSizeY() As Double Implements ICameraV2.PixelSizeY
        Get
            TL.LogMessage("PixelSizeY Get", pixelSize.ToString())
            Return pixelSize
        End Get
    End Property

    Public Sub PulseGuide(Direction As GuideDirections, Duration As Integer) Implements ICameraV2.PulseGuide
        TL.LogMessage("PulseGuide", "Not implemented - " & Direction.ToString)
        Throw New ASCOM.MethodNotImplementedException("Direction")
    End Sub

    Public Property ReadoutMode() As Short Implements ICameraV2.ReadoutMode
        Get
            TL.LogMessage("ReadoutMode Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ReadoutMode", False)
        End Get
        Set(value As Short)
            TL.LogMessage("ReadoutMode Set", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ReadoutMode", True)
        End Set
    End Property

    Public ReadOnly Property ReadoutModes() As ArrayList Implements ICameraV2.ReadoutModes
        Get
            TL.LogMessage("ReadoutModes Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("ReadoutModes", False)
        End Get
    End Property

    Public ReadOnly Property SensorName() As String Implements ICameraV2.SensorName
        Get
            TL.LogMessage("SensorName Get", "Not implemented")
            Throw New ASCOM.PropertyNotImplementedException("SensorName", False)
        End Get
    End Property

    Public ReadOnly Property SensorType() As SensorType Implements ICameraV2.SensorType
        Get
            TL.LogMessage("SensorType Get", "Not implemented")
            ' Throw New ASCOM.PropertyNotImplementedException("SensorType", False)
            Return SensorType.Monochrome
        End Get
    End Property

    Public Property SetCCDTemperature() As Double Implements ICameraV2.SetCCDTemperature
        Get
            TL.LogMessage("SetCCDTemperature Get", "Not implemented")
            'Throw New ASCOM.PropertyNotImplementedException("SetCCDTemperature", False)
            Return 0
        End Get
        Set(value As Double)
            TL.LogMessage("SetCCDTemperature Set", "Not implemented")
            'Throw New ASCOM.PropertyNotImplementedException("SetCCDTemperature", True)
            value = value
        End Set
    End Property
    Public Sub ImageReceived(ts As Integer) Handles FG.ImageReceivedExt
        If Not capturing Then
            FG.Acquisition = 0
            Exit Sub
        End If
        'ts
        'FG.Acquisition = 0
        Debug.Print("got image " & CStr(ts))
        'get frame from camera
        Dim w, h As Integer
        Dim byteArr() As Byte
        Dim d As Integer

        d = FG.GetBitPerPixel()
        w = FG.SizeX
        h = FG.SizeY

        ' FileNumber = FreeFile()

        If (d = 8) Then
            ReDim byteArr(w * h)
            byteArr = FG.GetRawData(0)

        ElseIf (d = 12) Then
            ReDim byteArr(w * h * 2)
            byteArr = FG.GetRawData(0)

        ElseIf (d = 16) Then
            ReDim byteArr(w * h * 2)
            byteArr = FG.GetRawData(0)
        End If

        ReDim cameraImageArray(w - 1, h - 1)
        cameraImageArray = ConvertFrameToImageAray(byteArr, w, h, d)
        cameraImageReady = True
        capturing = False
    End Sub
    Private Shared Function ConvertFrameToImageAray(ByVal frame As Byte(), w As Integer, h As Integer, d As Integer) As Object
        Dim imgArr As Integer(,)
        Dim pixelX As Integer = 0
        Dim pixelY As Integer = 0
        ReDim imgArr(w - 1, h - 1)
        If frame Is Nothing Then
            Debug.WriteLine("frame is nothing")
            Throw New ArgumentNullException("frame")
        End If

        Select Case d
            Case 12, 16
                Try

                    For y As Integer = 0 To CInt(h) - 1
                        pixelX = 0
                        For x As Integer = 0 To CInt(w * 2) - 1 Step 2
                            'imgArr(pixelX, y) = frame.Buffer(x + y * frame.Width)

                            imgArr(pixelX, y) = (frame(x + y * w * 2) + frame(x + y * w * 2) * 256)  ' stretch to 16bits

                            pixelX = pixelX + 1

                        Next

                    Next
                    Return imgArr
                Catch ex As Exception

                    MsgBox(ex.Message)
                End Try
            Case 8
                Try
                    Dim t As Integer
                    For y As Integer = 0 To CInt(h) - 1
                        pixelY = 0
                        For x As Integer = 0 To CInt(w) - 1
                            imgArr(x, y) = frame(x + y * (w))

                            'If (x + y * h) < 1443520 / 2 Then
                            '    imgArr(pixelX, y) = 0
                            'Else
                            '    imgArr(pixelX, y) = 255
                            'End If

                            ' imgArr(pixelX, y) = (frame.Buffer(x + y * frame.Width * 2) + frame.Buffer(x + 1 + y * frame.Width * 2) * 256)  ' stretch to 16bits

                            pixelX = pixelX + 1
                            t = t + 1
                        Next

                    Next
                    'For x As Integer = 0 To CInt(w) - 1
                    '    pixelY = 0
                    '    For y As Integer = 0 To CInt(h) - 1
                    '        imgArr(x, pixelY) = frame(x + y * h)

                    '        ' imgArr(pixelX, y) = (frame.Buffer(x + y * frame.Width * 2) + frame.Buffer(x + 1 + y * frame.Width * 2) * 256)  ' stretch to 16bits

                    '        pixelY = pixelY + 1

                    '    Next

                    'Next
                    Return imgArr
                Catch ex As Exception

                    MsgBox(ex.Message)
                End Try

            Case Else
                Throw New Exception("Current pixel format is not supported by this example (only Mono8 and BRG8Packed are supported).")
        End Select


    End Function
    Public Sub StartExposure(Duration As Double, Light As Boolean) Implements ICameraV2.StartExposure
        If capturing Then Exit Sub
        If (Duration < 0.0) Then Throw New InvalidValueException("StartExposure", Duration.ToString(), "0.0 upwards")
        'If (cameraNumX > ccdWidth) Then Throw New InvalidValueException("StartExposure", cameraNumX.ToString(), ccdWidth.ToString())
        'If (cameraNumY > ccdHeight) Then Throw New InvalidValueException("StartExposure", cameraNumY.ToString(), ccdHeight.ToString())
        'If (cameraStartX > ccdWidth) Then Throw New InvalidValueException("StartExposure", cameraStartX.ToString(), ccdWidth.ToString())
        'If (cameraStartY > ccdHeight) Then Throw New InvalidValueException("StartExposure", cameraStartY.ToString(), ccdHeight.ToString())

        cameraLastExposureDuration = Duration
        exposureStart = DateTime.Now
        'get single frame

        ExposureTime = Duration

        FG.Acquisition = 1
        ' FG.OneShot()
        capturing = True
        Debug.Print("start exposure")
        '

        TL.LogMessage("StartExposure", Duration.ToString() + " " + Light.ToString())

    End Sub

    Public Property StartX() As Integer Implements ICameraV2.StartX
        Get
            TL.LogMessage("StartX Get", cameraStartX.ToString())
            Return cameraStartX
        End Get
        Set(value As Integer)
            cameraStartX = value
            TL.LogMessage("StartX set", value.ToString())
        End Set
    End Property

    Public Property StartY() As Integer Implements ICameraV2.StartY
        Get
            TL.LogMessage("StartY Get", cameraStartY.ToString())
            Return cameraStartY
        End Get
        Set(value As Integer)
            cameraStartY = value
            TL.LogMessage("StartY set", value.ToString())
        End Set
    End Property

    Public Sub StopExposure() Implements ICameraV2.StopExposure
        FG.Acquisition = 0

        Debug.Print("stop exposure")
        TL.LogMessage("StopExposure", "Not implemented")
        Throw New MethodNotImplementedException("StopExposure")
    End Sub

#End Region

#Region "Private properties and methods"
    ' here are some useful properties and methods that can be used as required
    ' to help with

#Region "ASCOM Registration"

    Private Shared Sub RegUnregASCOM(ByVal bRegister As Boolean)

        Using P As New Profile() With {.DeviceType = "Camera"}
            If bRegister Then
                P.Register(driverID, driverDescription)
            Else
                P.Unregister(driverID)
            End If
        End Using

    End Sub

    <ComRegisterFunction()>
    Public Shared Sub RegisterASCOM(ByVal T As Type)

        RegUnregASCOM(True)

    End Sub

    <ComUnregisterFunction()>
    Public Shared Sub UnregisterASCOM(ByVal T As Type)

        RegUnregASCOM(False)

    End Sub

#End Region

    ''' <summary>
    ''' Returns true if there is a valid connection to the driver hardware
    ''' </summary>
    Private ReadOnly Property IsConnected As Boolean
        Get
            ' TODO check that the driver hardware connection exists and is connected to the hardware
            Return connectedState
        End Get
    End Property

    ''' <summary>
    ''' Use this function to throw an exception if we aren't connected to the hardware
    ''' </summary>
    ''' <param name="message"></param>
    Private Sub CheckConnected(ByVal message As String)
        If Not IsConnected Then
            Throw New NotConnectedException(message)
        End If
    End Sub

    ''' <summary>
    ''' Read the device configuration from the ASCOM Profile store
    ''' </summary>
    Friend Sub ReadProfile()
        Using driverProfile As New Profile()
            driverProfile.DeviceType = "Camera"
            traceState = Convert.ToBoolean(driverProfile.GetValue(driverID, traceStateProfileName, String.Empty, traceStateDefault))
            comPort = driverProfile.GetValue(driverID, comPortProfileName, String.Empty, comPortDefault)
        End Using
    End Sub

    ''' <summary>
    ''' Write the device configuration to the  ASCOM  Profile store
    ''' </summary>
    Friend Sub WriteProfile()
        Using driverProfile As New Profile()
            driverProfile.DeviceType = "Camera"
            driverProfile.WriteValue(driverID, traceStateProfileName, traceState.ToString())
            driverProfile.WriteValue(driverID, comPortProfileName, comPort.ToString())
        End Using

    End Sub

#End Region

End Class
