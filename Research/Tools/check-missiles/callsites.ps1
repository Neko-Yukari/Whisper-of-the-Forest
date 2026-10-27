param(
  [Parameter(Mandatory=$true)][string]$Assembly,
  [Parameter(Mandatory=$true)][string]$Needle
)

Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'

$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Assembly)
$hits = New-Object System.Collections.Generic.HashSet[string]

function Walk($types) {
  foreach ($t in $types) {
    if ($t.HasMethods) {
      foreach ($m in $t.Methods) {
        if (-not $m.HasBody) { continue }
        foreach ($i in $m.Body.Instructions) {
          $op = $i.Operand
          if ($op -and $op.ToString().Contains($Needle)) {
            [void]$hits.Add("$($t.FullName)::$($m.Name)  ->  $($op.ToString())")
          }
        }
      }
    }
    if ($t.HasNestedTypes) { Walk $t.NestedTypes }
  }
}

Walk $asm.MainModule.Types
$hits
