param(
    [string]$ProjectPath = "C:\Users\Marco\source\repos\MiApi",   # Ruta de tu proyecto
    [string]$PublishFolder = "bin\Release\net8.0\linux-x64\publish", # Ajusta según tu versión .NET
    [string]$VmUser = "usuario",       # Usuario de tu VM
    [string]$VmHost = "192.168.1.50",  # IP o dominio de tu VM
    [string]$VmPath = "/var/www/miapi",# Carpeta destino en la VM
    [string]$ServiceName = "miapi"     # Nombre del servicio systemd
)

Write-Host "🚀 Publicando API..."
dotnet publish $ProjectPath -c Release -r linux-x64 /p:PublishTrimmed=true

Write-Host "📂 Copiando archivos a la VM..."
scp -r "$ProjectPath\$PublishFolder\*" "$VmUser@$VmHost:$VmPath/"

Write-Host "🔧 Ajustando permisos y reiniciando servicio..."
ssh "$VmUser@$VmHost" "sudo chown -R www-data:www-data $VmPath && sudo chmod -R 755 $VmPath && sudo systemctl restart $ServiceName && sudo systemctl reload nginx"

Write-Host "✅ Deploy completado"
