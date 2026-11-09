using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
if(args.Length!=2)throw new ArgumentException("Usage: SignIdentity <signtool.exe> <publish directory>");
string directory=Path.GetFullPath(args[1]),package=Path.Combine(directory,"GlassDock.LocalPreview.identity.msix");
using var rsa=RSA.Create(2048);
var request=new CertificateRequest("CN=GlassDock Local",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature,true));
request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection {new Oid("1.3.6.1.5.5.7.3.3")},true));
using var certificate=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddYears(1));
string temporaryKey=Path.Combine(Path.GetTempPath(),"GlassDock-signing-"+Guid.NewGuid().ToString("N")+".pfx"),password=Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
try
{
    File.WriteAllBytes(temporaryKey,certificate.Export(X509ContentType.Pfx,password));
    File.WriteAllBytes(Path.Combine(directory,"GlassDock.Local.cer"),certificate.Export(X509ContentType.Cert));
    var start=new ProcessStartInfo(Path.GetFullPath(args[0])) {UseShellExecute=false,CreateNoWindow=true};
    foreach(string value in new[]{"sign","/fd","SHA256","/f",temporaryKey,"/p",password,package})start.ArgumentList.Add(value);
    using var process=Process.Start(start)!;process.WaitForExit();if(process.ExitCode!=0)throw new Exception("MSIX signing failed");
    Console.WriteLine($"Signed local GlassDock identity. Subject={certificate.Subject}; Thumbprint={certificate.Thumbprint}; No trust store modified.");
}
finally {if(File.Exists(temporaryKey))File.Delete(temporaryKey);}
