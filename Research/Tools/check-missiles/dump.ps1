param(
  [Parameter(Mandatory=$true)][string]$Assembly,
  [string]$Filter = '',
  [string]$Out = '',
  [switch]$BaseTypesOnly
)

Add-Type -Path 'C:/Users/81004/.dotnet/tools/.store/dotnet-ildasm/0.12.2/dotnet-ildasm/0.12.2/tools/netcoreapp3.0/any/Mono.Cecil.dll'

$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Assembly)
$sb = [System.Text.StringBuilder]::new()

function Fmt-TypeRef($t) {
  if ($null -eq $t) { return '' }
  if ($t -is [Mono.Cecil.GenericInstanceType]) {
    $args = ($t.GenericArguments | ForEach-Object { Fmt-TypeRef $_ }) -join ', '
    return "$($t.ElementType.FullName)<$args>"
  }
  return $t.FullName
}

function Fmt-Method($m) {
  $vis = if ($m.IsPublic) { 'public' } elseif ($m.IsFamily) { 'protected' } elseif ($m.IsAssembly) { 'internal' } elseif ($m.IsFamilyOrAssembly) { 'protected internal' } else { 'private' }
  $st = if ($m.IsStatic) { 'static ' } else { '' }
  $vt = if ($m.IsVirtual) { 'virtual ' } else { '' }
  $gp = if ($m.HasGenericParameters) { '<' + (($m.GenericParameters | ForEach-Object { $_.Name }) -join ',') + '>' } else { '' }
  $ps = ($m.Parameters | ForEach-Object { "$(Fmt-TypeRef $_.ParameterType) $($_.Name)" }) -join ', '
  return "    $vis $st$vt$(Fmt-TypeRef $m.ReturnType) $($m.Name)$gp($ps)"
}

function Dump-Type($t) {
  $kind = if ($t.IsInterface) { 'interface' } elseif ($t.IsEnum) { 'enum' } elseif ($t.IsValueType) { 'struct' } else { 'class' }
  $attrs = @()
  if ($t.IsAbstract -and -not $t.IsInterface) { $attrs += 'abstract' }
  if ($t.IsSealed -and -not $t.IsValueType) { $attrs += 'sealed' }
  $base = ''
  if ($t.BaseType) { $base = ' : ' + (Fmt-TypeRef $t.BaseType) }
  $ifaces = ''
  if ($t.Interfaces.Count -gt 0) { $ifaces = ', ' + (($t.Interfaces | ForEach-Object { Fmt-TypeRef $_.InterfaceType }) -join ', ') }
  [void]$sb.AppendLine("$kind $($t.FullName)$base$ifaces  [$($attrs -join ' ')]")
  if ($BaseTypesOnly) { [void]$sb.AppendLine(''); return }
  if ($t.HasFields) {
    foreach ($f in $t.Fields) {
      $fvis = if ($f.IsPublic) { 'public' } elseif ($f.IsFamily) { 'protected' } elseif ($f.IsAssembly) { 'internal' } else { 'private' }
      $fst = if ($f.IsStatic) { 'static ' } else { '' }
      [void]$sb.AppendLine("  field $fvis $fst$(Fmt-TypeRef $f.FieldType) $($f.Name)")
    }
  }
  if ($t.HasProperties) {
    foreach ($p in $t.Properties) {
      [void]$sb.AppendLine("  prop $(Fmt-TypeRef $p.PropertyType) $($p.Name)")
    }
  }
  if ($t.HasMethods) {
    foreach ($m in $t.Methods) { [void]$sb.AppendLine((Fmt-Method $m)) }
  }
  [void]$sb.AppendLine('')
}

function Walk($types) {
  foreach ($t in $types) {
    if ($Filter -eq '' -or $t.FullName -match $Filter) { Dump-Type $t }
    if ($t.HasNestedTypes) { Walk $t.NestedTypes }
  }
}

Walk $asm.MainModule.Types

if ($Out -ne '') {
  [System.IO.File]::WriteAllText($Out, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
  "wrote $Out ($($sb.Length) chars)"
} else {
  $sb.ToString()
}
