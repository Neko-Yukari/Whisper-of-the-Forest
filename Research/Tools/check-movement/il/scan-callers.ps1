Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly('E:/SteamLibrary/steamapps/common/RimWorld/Assembly-CSharp.dll')

function Scan-Module($module, $targets) {
  foreach ($type in $module.GetTypes()) {
    foreach ($method in $type.Methods) {
      if (-not $method.HasBody) { continue }
      foreach ($instr in $method.Body.Instructions) {
        $operand = $instr.Operand
        if ($operand -is [Mono.Cecil.MethodReference] -and $targets -contains $operand.Name) {
          "{0}::{1} calls {2}" -f $type.FullName, $method.Name, $operand.Name
        }
      }
    }
  }
}

Scan-Module $asm.MainModule @('TryEnterNextPathCell', 'SetupMoveIntoNextCell', 'GetPawnCellBaseCostOverride', 'CostToMoveIntoCell')
