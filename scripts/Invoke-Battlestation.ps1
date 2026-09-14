param([Parameter(Mandatory=$true)][string]$Command,[switch]$Terminal,[string]$PipeName)
$ErrorActionPreference='Stop'
$name=if($PipeName){$PipeName}elseif($Terminal){'Battlestation.NativeTerminal.v1'}else{'Battlestation.Control'}
$pipe=[IO.Pipes.NamedPipeClientStream]::new('.',$name,[IO.Pipes.PipeDirection]::InOut)
try {
    $pipe.Connect(3000)
    $encoding=[Text.UTF8Encoding]::new($false)
    $writer=[IO.StreamWriter]::new($pipe,$encoding,1024,$true)
    $reader=[IO.StreamReader]::new($pipe,$encoding,$false,1024,$true)
    try {
        $writer.AutoFlush=$true;$writer.WriteLine($Command)
        $read=$reader.ReadLineAsync()
        if(!$read.Wait(5000)){throw 'Command response still pending'}
        $read.Result
    }finally{$reader.Dispose();$writer.Dispose()}
}finally{$pipe.Dispose()}
