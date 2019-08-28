// $todo(jeff.lill): 
//
// This entire program is a big hack.  I eventually need to come
// back and implement something cleaner.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Renci.SshNet;

using MongoDB.Bson;
using MongoDB.Driver;
using System.Net;

namespace Deployer
{
    public class UploadFileInfo
    {
        public string Name { get; set; }
        public string Contents { get; set; }
        public bool IsEnabled { get; set; }

        public UploadFileInfo()
        {
            IsEnabled = true;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class CommonConfig
    {
        public string   ServiceNamePrefix = "lill-dev-com";
        public string   ServiceDomain     = "lilltek.net";
        public string   Datacenter        = "HOME";
        public string   AdminUser         = "spot";
        public string   AdminPassword     = "WagTheDog!";
        public int      RaidChunkSizeKB   = 64;
        public int      ReadAheadSizeKB   = 0;
        public string   LogstashVersion   = "1:1.5.2-1";
        public bool     UpgradeLinux      = false;                  // Set to FALSE when debugging setup scripts/config for faster turnaround
        public bool     Azure             = false;
        public bool     PortMapping       = false;                  // Set to TRUE to enable port mapping for Azure etc.
    }

    public class CassandraConfig
    {
        public string   CassandraVersion          = "2.2.0-1";
        public string   ClusterName               = "TEST";
        public string   OpscenterVmType           = "Local_4GB_SSD";
        public string   DataVmType                = "Local_4GB_SSD";
        public bool     EnableOpscenter           = true;
        public int      NodeCount                 = 3;
        public int      RackCount                 = 1;
        public int      TokenCount                = 256;            // Tokens per node
        public bool     WipeAndReconfig           = false;
        public bool     WipeData                  = false;
        public double   FreeMemoryGB              = 4;
        public int      MaxMemMapCount            = 131072;
        public int      NativeTransportMaxThreads = 256;            // Default is 128
        public TimeSpan ClusterJoinDelay          = TimeSpan.FromMinutes(2);

        // These fields are computed.

        public int      MaxHeapSizeMB;
        public int      MaxNewHeapSizeMB;
        public int      RowCacheSizeMB;
    }

    public class RedisConfig
    {
        public string   RedisVersion       = "3.0.3";
        public string   DataVmType         = "Local_4GB";
        public int      NodeCount          = 3;
        public bool     Replicate          = false;                 // Indicates that slave replicas will be deployed to the cluster.
        public TimeSpan ClusterNodeTimeout = TimeSpan.FromMilliseconds(15000);

        // These fields are computed.

        public int      MaxMemoryMB;
    }

    public class MongoConfig
    {
        public string   MongoVersion      = "3.0.4";
        public string   PatchMongoVersion = "3.1.5";
        public bool     PatchMongo        = false;
        public string   RouterVmType      = "Local_4GB_SSD";
        public string   DataVmType        = "Local_4GB_SSD";
        public int      LogVerbosity      = 0;
        public bool     JournalEnabled    = true;
        public int      RouterCount       = 4;
        public int      ShardCount        = 3;
        public int      ReplicaCount      = 3;
        public int      ShardChunkSizeMB  = 256;
        public bool     WipeAndReconfig   = false;
        public double   FreeMemoryGB      = 4;

        // TokuMX specific settings.

        public bool     InstallTokuMX     = true;
        public string   TokuVersion       = "2.0.1-1";
        public bool     TokuDirectIO      = true;

        // These fields are computed.

        public string   ClusterKey;
        public int      CacheSizeGB;
    }

public class ElasticConfig
    {
        public string   KibanaUser        = "spot";
        public string   KibanaPassword    = "WagTheDog!";
        public string   ElasticUser       = "spot";
        public string   ElasticPassword   = "WagTheDog!";
        public string   ElasticVersion    = "1.7.2";
        public string   KibanaVersion     = "4.1.2";
        public string   NginxVersion      = "1.8.0-1~trusty";
        public string   RouterVmType      = "Local_4GB_SSD";
        public string   DataVmType        = "Local_4GB_SSD";
        public int      RouterCount       = 1;
        public int      NodeCount         = 1;
        public int      ShardCount        = 5;
        public int      ReplicaCount      = 1;
        public int      MaxMemMapCount    = 131072;
        public TimeSpan DiscoveryTimeout  = TimeSpan.FromSeconds(3);
        public int      MaxDiscoverySeeds = 3;

        // These fields are computed.

        public int      HeapSizeMB;
    }

    public class VmInfo
    {
        public string   Name;
        public int      Cores;
        public double   RamGB;
        public bool     LocalSSD;

        private static Dictionary<string, VmInfo> vmTypes;

        static VmInfo()
        {
            vmTypes = new Dictionary<string, VmInfo>(StringComparer.InvariantCultureIgnoreCase);

            Add(vmTypes, name: "Local_2GB", cores: 4, ramGB: 2);
            Add(vmTypes, name: "Local_4GB", cores: 4, ramGB: 4);
            Add(vmTypes, name: "Local_8GB", cores: 4, ramGB: 8);

            Add(vmTypes, name: "Local_2GB_SSD", cores: 4, ramGB: 2, localSSD: true);
            Add(vmTypes, name: "Local_4GB_SSD", cores: 4, ramGB: 4, localSSD: true);
            Add(vmTypes, name: "Local_8GB_SSD", cores: 4, ramGB: 8, localSSD: true);

            Add(vmTypes, name: "ExtraSmall", cores: 1, ramGB: 0.75);
            Add(vmTypes, name: "Small", cores: 1, ramGB: 1.75);
            Add(vmTypes, name: "Medium", cores: 2, ramGB: 3.5);
            Add(vmTypes, name: "Large", cores: 4, ramGB: 7);
            Add(vmTypes, name: "ExtraLarge", cores: 8, ramGB: 14);

            Add(vmTypes, name: "Standard_A0", cores: 1, ramGB: 0.75);
            Add(vmTypes, name: "Standard_A1", cores: 1, ramGB: 1.75);
            Add(vmTypes, name: "Standard_A2", cores: 2, ramGB: 3.5);
            Add(vmTypes, name: "Standard_A3", cores: 4, ramGB: 7);
            Add(vmTypes, name: "Standard_A4", cores: 8, ramGB: 14);
            Add(vmTypes, name: "Standard_A5", cores: 2, ramGB: 14);
            Add(vmTypes, name: "Standard_A6", cores: 4, ramGB: 28);
            Add(vmTypes, name: "Standard_A7", cores: 8, ramGB: 56);
            Add(vmTypes, name: "Standard_A8", cores: 8, ramGB: 56);
            Add(vmTypes, name: "Standard_A9", cores: 18, ramGB: 112);
            Add(vmTypes, name: "Standard_A10", cores: 8, ramGB: 56);
            Add(vmTypes, name: "Standard_A11", cores: 16, ramGB: 112);

            Add(vmTypes, name: "Standard_D1", cores: 1, ramGB: 3.5, localSSD: true);
            Add(vmTypes, name: "Standard_D2", cores: 2, ramGB: 7, localSSD: true);
            Add(vmTypes, name: "Standard_D3", cores: 4, ramGB: 14, localSSD: true);
            Add(vmTypes, name: "Standard_D4", cores: 8, ramGB: 28, localSSD: true);
            Add(vmTypes, name: "Standard_D11", cores: 2, ramGB: 14, localSSD: true);
            Add(vmTypes, name: "Standard_D12", cores: 4, ramGB: 28, localSSD: true);
            Add(vmTypes, name: "Standard_D13", cores: 8, ramGB: 56, localSSD: true);
            Add(vmTypes, name: "Standard_D14", cores: 16, ramGB: 112, localSSD: true);

            Add(vmTypes, name: "Standard_DS1", cores: 1, ramGB: 3.5, localSSD: true);
            Add(vmTypes, name: "Standard_DS2", cores: 2, ramGB: 7, localSSD: true);
            Add(vmTypes, name: "Standard_DS3", cores: 4, ramGB: 14, localSSD: true);
            Add(vmTypes, name: "Standard_DS4", cores: 8, ramGB: 28, localSSD: true);
            Add(vmTypes, name: "Standard_DS11", cores: 2, ramGB: 14, localSSD: true);
            Add(vmTypes, name: "Standard_DS12", cores: 4, ramGB: 28, localSSD: true);
            Add(vmTypes, name: "Standard_DS13", cores: 8, ramGB: 56, localSSD: true);
            Add(vmTypes, name: "Standard_DS14", cores: 16, ramGB: 112, localSSD: true);

            Add(vmTypes, name: "Standard_G1", cores: 2, ramGB: 28, localSSD: true);
            Add(vmTypes, name: "Standard_G2", cores: 4, ramGB: 56, localSSD: true);
            Add(vmTypes, name: "Standard_G3", cores: 8, ramGB: 112, localSSD: true);
            Add(vmTypes, name: "Standard_G4", cores: 16, ramGB: 224, localSSD: true);
            Add(vmTypes, name: "Standard_G5", cores: 32, ramGB: 448, localSSD: true);
        }

        private static void Add(Dictionary<string, VmInfo> info, string name, int cores, double ramGB, bool localSSD = false)
        {
            info.Add(name, new VmInfo() { Name = name, Cores = cores, RamGB = ramGB, LocalSSD = localSSD });
        }

        public static VmInfo GetVmInfo(string vmName)
        {
            return vmTypes[vmName];
        }
    }

    public static class Program
    {
        private static List<Server>     servers         = new List<Server>();
        public static string            logFolder;

        // Cluster settings.

        private static CommonConfig     commonConfig    = new CommonConfig();
        private static CassandraConfig  cassandraConfig = new CassandraConfig();
        private static RedisConfig      redisConfig     = new RedisConfig();
        private static MongoConfig      mongoConfig     = new MongoConfig();
        private static ElasticConfig    elasticConfig   = new ElasticConfig();

        static void Main(string[] args)
        {
            DeployRedis();
            //DeployCassandra();
            //DeployElastic();
            //DeployMongo();

            //RunCommands(
            //    () => { InitializeCassandraServers(); },
            //    () => { DisplayCassandraStatus(waitForReady: true); },
            //    server =>
            //    {
            //        using (var sshClient = server.ConnectSSh())
            //        {
            //            server.Status = "Stopping Cassandra...";

            //            server.RunCommand(sshClient, "sudo stop cassandra");
            //        }

            //        return true; // Return TRUE to reboot.
            //    });

            //RunCommands(
            //    () => { InitializeCassandraServers(); },
            //    () => { DisplayCassandraStatus(waitForReady: true); },
            //    server =>
            //    {
            //        using (var sshClient = server.ConnectSSh())
            //        {
            //            server.Status = "Clearing disk cache...";

            //            server.RunCommand(sshClient, "sudo bash -c 'sync; echo 3 > /proc/sys/vm/drop_caches'");
            //        }

            //        return false; // Return TRUE to reboot.
            //    });

            Debugger.Break();
        }

        public enum ServerType
        {
            MongoData,
            MongoRouter,
            ElasticData,
            ElasticRouter,
            CassandraNode,
            CassandraOpsCenter,
            Redis
        }

        public class Server
        {
            public VmInfo       VmInfo { get; private set; }
            public string       Datacenter { get; set; }
            public string       Name { get; set; }
            public string       Host { get; set; }
            public ServerType   Type { get; set; }
            public string       Status { get; set; }
            public int          SshPort { get; set; }
            public int          DataPort { get; set; }
            public int          ConfPort { get; set; }
            public string       ReplicaSet { get; set; }
            public bool         IsReady { get; set; }
            public int          RouterId { get; set; }
            public int          ShardId { get; set; }
            public int          ReplicaId { get; set; }
            public int          NodeId { get; set; }
            public string       Rack { get; set; }
            public string       VNetIP { get; set; }
            public int          HashSlotFirst { get; set;}
            public int          HashSlotLast { get; set;}

            public bool LocalSSD
            {
                get { return VmInfo.LocalSSD; }
            }

            public Server(string vmType)
            {
                this.VmInfo = VmInfo.GetVmInfo(vmType);
            }

            public void WaitForBoot()
            {
                while (true)
                {
                    using (var sshClient = new SshClient(new ConnectionInfo(Host, SshPort, commonConfig.AdminUser, new PasswordAuthenticationMethod(commonConfig.AdminUser, commonConfig.AdminPassword))))
                    {
                        try
                        {
                            sshClient.Connect();
                            break;
                        }
                        catch
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(10));
                        }
                    }
                }

                Status = "VM running";
            }

            public void WaitForMongod()
            {
                Status = "Waiting for MONGOD";

                while (true)
                {
                    try
                    {
                        var mongoClient = new MongoClient(string.Format("mongodb://{0}:{1}", Host, DataPort));

                        mongoClient.GetDatabase("admin");
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                    }
                }

                Status = "MONGOD ready";
            }

            public void WaitForMongoc()
            {
                Status = "Waiting for MONGOC";

                while (true)
                {
                    try
                    {
                        var mongoClient = new MongoClient(string.Format("mongodb://{0}:{1}", Host, DataPort));

                        mongoClient.GetDatabase("admin");
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                    }
                }

                Status = "MONGOC ready";
            }

            public void WaitForMongos()
            {
                Status = "Waiting for MONGOS";

                while (true)
                {
                    try
                    {
                        var mongoClient = new MongoClient(string.Format("mongodb://{0}:{1}", Host, ConfPort));

                        mongoClient.GetDatabase("admin");
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(10));
                    }
                }

                Status = "MONGOS ready";
            }

            public bool RunCommand(SshClient client, string format, params object[] args)
            {
                var logPath = Path.Combine(logFolder, string.Format("{0}.log", Name));
                var command = string.Format(format, args);

                using (var logOutput = File.AppendText(logPath))
                {
                    logOutput.WriteLine("START: {0}", command);

                    var result = client.RunCommand(command);

                    using (var reader = new StreamReader(result.OutputStream))
                    {
                        var line = reader.ReadLine();

                        while (line != null)
                        {
                            logOutput.WriteLine("    " + line);
                            line = reader.ReadLine();
                        }
                    }

                    logOutput.WriteLine("EXTENDED:");

                    using (var reader = new StreamReader(result.ExtendedOutputStream))
                    {
                        var line = reader.ReadLine();

                        while (line != null)
                        {
                            logOutput.WriteLine("    " + line);
                            line = reader.ReadLine();
                        }
                    }

                    if (result.ExitStatus == 0)
                    {
                        logOutput.WriteLine("END [OK] {0}", command);
                    }
                    else
                    {
                        logOutput.WriteLine("END [ERROR={0}]: {1}", result.ExitStatus, command);
                    }

                    if (result.ExitStatus != 0)
                    {
                        Status = string.Format("ERROR[{0}]: {1}", result.ExitStatus, command);
                        return false;
                    }

                    return true;
                }
            }

            public SshClient ConnectSSh()
            {
                var conInfo   = new ConnectionInfo(Host, SshPort, commonConfig.AdminUser, new PasswordAuthenticationMethod(commonConfig.AdminUser, commonConfig.AdminPassword));
                var sshClient = new SshClient(conInfo);

                try
                {
                    sshClient.Connect();
                }
                catch
                {
                    sshClient.Dispose();
                    throw;
                }

                return sshClient;
            }

            public ScpClient ConnectScp()
            {
                var conInfo   = new ConnectionInfo(Host, SshPort, commonConfig.AdminUser, new PasswordAuthenticationMethod(commonConfig.AdminUser, commonConfig.AdminPassword));
                var scpClient = new ScpClient(conInfo);

                try
                {
                    scpClient.Connect();
                }
                catch
                {
                    scpClient.Dispose();
                    throw;
                }

                return scpClient;
            }

            public void UploadFile(ScpClient scpClient, string text, string targetPath, bool convertTabs = false)
            {
                var data = Encoding.ASCII.GetBytes(text);

                UploadFile(scpClient, data, targetPath, isText: true, convertTabs: convertTabs);
            }

            public void UploadFile(ScpClient scpClient, byte[] data, string targetPath, bool isText = false, bool convertTabs = false)
            {
                if (isText)
                {
                    var text = Encoding.ASCII.GetString(data);

                    text = text.Replace("\r\n", "\n");

                    if (convertTabs)
                    {
                        text = text.Replace("\t", "    ");
                    }

                    data = Encoding.ASCII.GetBytes(text);
                }

                scpClient.Upload(new MemoryStream(data), targetPath);
            }

            public string SetupFolder
            {
                get { return string.Format("/home/{0}/setup/", commonConfig.AdminUser);  }
            }

            public static string AsText(string text)
            {
                return text;
            }

            public static string AsText(byte[] bytes)
            {
                // Linux really doesn't like the Unicode byte order markers that Visual Studio
                // often adds to the beginning of text files.  We're going strip these out if
                // present:
                //
                // reference: https://en.wikipedia.org/wiki/Byte_order_mark

                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    var cleanBytes = new byte[bytes.Length - 3];

                    Array.Copy(bytes, 3, cleanBytes, 0, bytes.Length - 3);
                    bytes = cleanBytes;
                }

                return Encoding.UTF8.GetString(bytes);
            }

            public List<UploadFileInfo> GetCommonFiles()
            {
                var list = new List<UploadFileInfo>();

                list.Add(new UploadFileInfo() { Name = "upgrade-linux.sh",                  Contents = AsText(Resources.upgrade_linux) });
                list.Add(new UploadFileInfo() { Name = "service-starter.sh",                Contents = AsText(Resources.service_starter) });
                list.Add(new UploadFileInfo() { Name = "setup-hosts.sh",                    Contents = AsText(Resources.setup_hosts) });
                list.Add(new UploadFileInfo() { Name = "setup-disk.sh",                     Contents = AsText(Resources.setup_disk) });
                list.Add(new UploadFileInfo() { Name = "setup-linux.sh",                    Contents = AsText(Resources.setup_linux) });
                list.Add(new UploadFileInfo() { Name = "setup-dotnet.sh",                   Contents = AsText(Resources.setup_dotnet) });
                list.Add(new UploadFileInfo() { Name = "setup-java.sh",                     Contents = AsText(Resources.setup_java) });
                list.Add(new UploadFileInfo() { Name = "setup-logstash.sh",                 Contents = AsText(Resources.setup_logstash) });
                list.Add(new UploadFileInfo() { Name = "logstash.init.conf",                Contents = AsText(Resources.logstash_init) });
                list.Add(new UploadFileInfo() { Name = "setup-clean.sh",                    Contents = AsText(Resources.setup_clean) });
                list.Add(new UploadFileInfo() { Name = "setup-ntp.sh",                      Contents = AsText(Resources.setup_ntp) });
                list.Add(new UploadFileInfo() { Name = "ntp.conf",                          Contents = AsText(Resources.ntp) });

                return list;
            }

            public void AddCassandraFiles(List<UploadFileInfo> list)
            {
                list.Add(new UploadFileInfo() { Name = "setup-cops.sh",                     Contents = AsText(Resources.setup_cops),                    IsEnabled = Type == ServerType.CassandraOpsCenter });
                list.Add(new UploadFileInfo() { Name = "opscenterd.conf",                   Contents = AsText(Resources.cassandra_opscenterd),          IsEnabled = Type == ServerType.CassandraOpsCenter });
                list.Add(new UploadFileInfo() { Name = "opscenter_cluster.conf",            Contents = AsText(Resources.cassandra_opscenter_cluster),   IsEnabled = Type == ServerType.CassandraOpsCenter });

                list.Add(new UploadFileInfo() { Name = "setup-cdb.sh",                      Contents = AsText(Resources.setup_cdb),                     IsEnabled = Type == ServerType.CassandraNode });
                list.Add(new UploadFileInfo() { Name = "setup-cassandra.sh",                Contents = AsText(Resources.setup_cassandra),               IsEnabled = Type == ServerType.CassandraNode });
                list.Add(new UploadFileInfo() { Name = "cassandra-rackdc.properties",       Contents = AsText(Resources.cassandra_rackdc),              IsEnabled = Type == ServerType.CassandraNode });
                list.Add(new UploadFileInfo() { Name = "cassandra.yaml",                    Contents = AsText(Resources.cassandra),                     IsEnabled = Type == ServerType.CassandraNode });
                //list.Add(new UploadFileInfo() { Name = "cassandra.init.conf",               Contents = AsText(Resources.cassandra_init),                IsEnabled = Type == ServerType.CassandraNode });
                list.Add(new UploadFileInfo() { Name = "cassandra.initd.conf",              Contents = AsText(Resources.cassandra_initd),               IsEnabled = Type == ServerType.CassandraNode });
                list.Add(new UploadFileInfo() { Name = "cassandra-env.sh",                  Contents = AsText(Resources.cassandra_env),                 IsEnabled = Type == ServerType.CassandraNode });
                list.Add(new UploadFileInfo() { Name = "commitlog_archiving.properties",    Contents = AsText(Resources.cassandra_commitlog_archiving), IsEnabled = Type == ServerType.CassandraNode });
                list.Add(new UploadFileInfo() { Name = "logback-tools.xml",                 Contents = Resources.cassandra_logback_tools,               IsEnabled = Type == ServerType.CassandraNode });
                list.Add(new UploadFileInfo() { Name = "logback.xml",                       Contents = Resources.cassandra_logback,                     IsEnabled = Type == ServerType.CassandraNode });
                list.Add(new UploadFileInfo() { Name = "setup-cdb.sh",                      Contents = AsText(Resources.setup_cdb),                     IsEnabled = Type == ServerType.CassandraNode });
                list.Add(new UploadFileInfo() { Name = "setup-agent.sh",                    Contents = AsText(Resources.setup_agent),                   IsEnabled = Type == ServerType.CassandraNode });
                list.Add(new UploadFileInfo() { Name = "address.yaml",                      Contents = AsText(Resources.agent_address),                 IsEnabled = Type == ServerType.CassandraNode });
            }

            public void AddRedisFiles(List<UploadFileInfo> list)
            {
                list.Add(new UploadFileInfo() { Name = "redis.conf",                        Contents = AsText(Resources.redis),                         IsEnabled = Type == ServerType.Redis });
                list.Add(new UploadFileInfo() { Name = "redis.initd.conf",                  Contents = AsText(Resources.redis_initd),                   IsEnabled = Type == ServerType.Redis });
                list.Add(new UploadFileInfo() { Name = "setup-rds.sh",                      Contents = AsText(Resources.setup_rds),                     IsEnabled = Type == ServerType.Redis });
                list.Add(new UploadFileInfo() { Name = "setup-redis.sh",                    Contents = AsText(Resources.setup_redis),                   IsEnabled = Type == ServerType.Redis });
                list.Add(new UploadFileInfo() { Name = "setup-cluster.sh",                  Contents = AsText(RedisClusterConfigScript()),              IsEnabled = Type == ServerType.Redis });
            }

            public void AddMongoFiles(List<UploadFileInfo> list)
            {
                list.Add(new UploadFileInfo() { Name = "setup-mongo.sh",                    Contents = AsText(Resources.setup_mongo) });
                list.Add(new UploadFileInfo() { Name = "setup-mongo-admin.sh",              Contents = AsText(Resources.setup_mongo_admin) });
                list.Add(new UploadFileInfo() { Name = "setup-toku-admin.sh",               Contents = AsText(Resources.setup_toku_admin) });
                list.Add(new UploadFileInfo() { Name = "log-processor.sh",                  Contents = AsText(Resources.log_processor) });
                list.Add(new UploadFileInfo() { Name = "patch-mongo.sh",                    Contents = AsText(Resources.patch_mongo) });
                list.Add(new UploadFileInfo() { Name = "unpatch-mongo.sh",                  Contents = AsText(Resources.unpatch_mongo) });
                list.Add(new UploadFileInfo() { Name = "cluster.key",                       Contents = mongoConfig.ClusterKey });

                list.Add(new UploadFileInfo() { Name = "mongoc.conf",                       Contents = AsText(Resources.mongoc),                        IsEnabled = Type == ServerType.MongoData });
                list.Add(new UploadFileInfo() { Name = "mongoc.init.conf",                  Contents = AsText(Resources.mongoc_init),                   IsEnabled = Type == ServerType.MongoData });
                list.Add(new UploadFileInfo() { Name = "mongod.init.conf",                  Contents = AsText(Resources.mongod_init),                   IsEnabled = Type == ServerType.MongoData });
                list.Add(new UploadFileInfo() { Name = "mongod.conf",                       Contents = AsText(Resources.mongod),                        IsEnabled = Type == ServerType.MongoData });
                list.Add(new UploadFileInfo() { Name = "setup-mdb.sh",                      Contents = AsText(Resources.setup_mdb),                     IsEnabled = Type == ServerType.MongoData });
                list.Add(new UploadFileInfo() { Name = "setup-shard.sh",                    Contents = MongoShardConfigScript(),                        IsEnabled = Type == ServerType.MongoData });

                list.Add(new UploadFileInfo() { Name = "mongos.conf",                       Contents = AsText(Resources.mongos),                        IsEnabled = Type == ServerType.MongoRouter });
                list.Add(new UploadFileInfo() { Name = "mongos.init.conf",                  Contents = AsText(Resources.mongos_init),                   IsEnabled = Type == ServerType.MongoRouter });
                list.Add(new UploadFileInfo() { Name = "setup-mqr.sh",                      Contents = AsText(Resources.setup_mqr),                     IsEnabled = Type == ServerType.MongoRouter });
                list.Add(new UploadFileInfo() { Name = "setup-router.sh",                   Contents = MongoRouterConfigScript(),                       IsEnabled = Type == ServerType.MongoRouter });
                list.Add(new UploadFileInfo() { Name = "upgrade-shards.sh",                 Contents = AsText(Resources.upgrade_shards),                IsEnabled = Type == ServerType.MongoRouter });

                // TokuMX files

                list.Add(new UploadFileInfo() { Name = "setup-tokumx.sh",                   Contents = AsText(Resources.setup_tokumx) });

                list.Add(new UploadFileInfo() { Name = "tokumxc.conf",                      Contents = AsText(Resources.tokumxc),                       IsEnabled = Type == ServerType.MongoData });
                list.Add(new UploadFileInfo() { Name = "tokumxc.init.conf",                 Contents = AsText(Resources.tokumxc_init),                  IsEnabled = Type == ServerType.MongoData });
                list.Add(new UploadFileInfo() { Name = "tokumxd.conf",                      Contents = AsText(Resources.tokumxd),                       IsEnabled = Type == ServerType.MongoData });
                list.Add(new UploadFileInfo() { Name = "tokumxd.init.conf",                 Contents = AsText(Resources.tokumxd_init),                  IsEnabled = Type == ServerType.MongoData });
                list.Add(new UploadFileInfo() { Name = "setup-tdb.sh",                      Contents = AsText(Resources.setup_tdb),                     IsEnabled = Type == ServerType.MongoData });

                list.Add(new UploadFileInfo() { Name = "tokumxs.conf",                      Contents = AsText(Resources.tokumxs),                       IsEnabled = Type == ServerType.MongoRouter });
                list.Add(new UploadFileInfo() { Name = "tokumxs.init.conf",                 Contents = AsText(Resources.tokumxs_init),                  IsEnabled = Type == ServerType.MongoRouter });
                list.Add(new UploadFileInfo() { Name = "setup-tqr.sh",                      Contents = AsText(Resources.setup_tqr),                     IsEnabled = Type == ServerType.MongoRouter });
            }

            public void AddElasticFiles(List<UploadFileInfo> list)
            {
                list.Add(new UploadFileInfo() { Name = "setup-elasticsearch.sh",            Contents = AsText(Resources.setup_elasticsearch) });
                list.Add(new UploadFileInfo() { Name = "elasticsearch.yml",                 Contents = AsText(Resources.elasticsearch_yml) });
                list.Add(new UploadFileInfo() { Name = "logging.yml",                       Contents = AsText(Resources.elasticsearch_logging_yml) });
                list.Add(new UploadFileInfo() { Name = "elasticsearch.init.conf",           Contents = AsText(Resources.elasticsearch_init) });

                list.Add(new UploadFileInfo() { Name = "nginx.conf",                        Contents = AsText(Resources.nginx),                         IsEnabled = Type == ServerType.ElasticRouter });
                list.Add(new UploadFileInfo() { Name = "setup-nginx-passwd.sh",             Contents = AsText(Resources.setup_nginx_passwd),            IsEnabled = Type == ServerType.ElasticRouter });
                list.Add(new UploadFileInfo() { Name = "kibana.init.conf",                  Contents = AsText(Resources.kibana_init),                   IsEnabled = Type == ServerType.ElasticRouter });
                list.Add(new UploadFileInfo() { Name = "kibana.yml",                        Contents = AsText(Resources.kibana),                        IsEnabled = Type == ServerType.ElasticRouter });
                list.Add(new UploadFileInfo() { Name = "setup-esr.sh",                      Contents = AsText(Resources.setup_esr),                     IsEnabled = Type == ServerType.ElasticRouter });

                list.Add(new UploadFileInfo() { Name = "setup-esd.sh",                      Contents = AsText(Resources.setup_esd),                     IsEnabled = Type == ServerType.ElasticData });
            }

            public bool ConfigureCassandra()
            {
                using (var sshClient = ConnectSSh())
                {
                    if (!RunCommand(sshClient, "mkdir -p {0}", SetupFolder))
                    {
                        return false;
                    }

                    using (var scpClient = ConnectScp())
                    {
                        Status = "Uploading setup...";

                        var files = GetCommonFiles();

                        AddCassandraFiles(files);

                        foreach (var file in files)
                        {
                            if (!file.IsEnabled)
                            {
                                continue;
                            }

                            var text = file.Contents;

                            // Replace macros in selected files.

                            switch (file.Name)
                            {
                                case "setup-hosts.sh":

                                    text = text.Replace("$(serverName)", Name);
                                    text = text.Replace("$(serviceDomain)", commonConfig.ServiceDomain);
                                    break;

                                case "setup-cdb.sh":

                                    const string cdbSetupVars =
@"CASSANDRA_VERSION={0}
LOGSTASH_VERSION={1}
RAID_CHUNK_SIZE_KB={2}
READ_AHEAD_SIZE_KB={3}
LOCAL_SSD={4}
";
                                    text = text.Replace("# @DEPLOYER-OVERRIDES@\r\n", string.Format(cdbSetupVars, cassandraConfig.CassandraVersion, 
                                                                                                                  commonConfig.LogstashVersion, 
                                                                                                                  commonConfig.RaidChunkSizeKB, 
                                                                                                                  commonConfig.ReadAheadSizeKB,
                                                                                                                  LocalSSD ? "true" : "false"));
                                    break;

                                case "setup-cops.sh":

                                    const string opsSetupVars =
@"CASSANDRA_VERSION={0}
LOGSTASH_VERSION={1}
RAID_CHUNK_SIZE_KB={2}
READ_AHEAD_SIZE_KB={3}
LOCAL_SSD={4}
";
                                    text = text.Replace("# @DEPLOYER-OVERRIDES@\r\n", string.Format(opsSetupVars, cassandraConfig.CassandraVersion,
                                                                                                                  commonConfig.LogstashVersion,
                                                                                                                  commonConfig.RaidChunkSizeKB,
                                                                                                                  commonConfig.ReadAheadSizeKB,
                                                                                                                  LocalSSD ? "true" : "false"));
                                    text = text.Replace("$(clusterName)", cassandraConfig.ClusterName);
                                    break;

                                case "cassandra-rackdc.properties":

                                    text = text.Replace("$(datacenter)", Datacenter);
                                    text = text.Replace("$(rack)", Rack);
                                    break;

                                case "cassandra.yaml":

                                    text = text.Replace("$(clusterName)", cassandraConfig.ClusterName);
                                    text = text.Replace("$(vnetIP)", VNetIP);
                                    text = text.Replace("$(tokenCount)", cassandraConfig.TokenCount.ToString());
                                    text = text.Replace("$(seedIPs)", GetCassandraSeeds());
                                    text = text.Replace("$(rowCacheSizeMB)", cassandraConfig.RowCacheSizeMB.ToString());
                                    text = text.Replace("$(nativeTransportMaxThreads)", cassandraConfig.NativeTransportMaxThreads.ToString());
                                    break;

                                case "setup-linux.sh":

                                    text = text.Replace("$(swapSizeMB)", GetSwapSizeMB(cassandraConfig.DataVmType).ToString());
                                    break;

                                case "address.yaml":

                                    var opsCenterServer = servers.SingleOrDefault(s => s.Type == ServerType.CassandraOpsCenter);

                                    text = text.Replace("$(nodeName)", Name);
                                    text = text.Replace("$(opsCenterIP)", opsCenterServer != null ? opsCenterServer.VNetIP : string.Empty);
                                    text = text.Replace("$(quotedSeedIPs)", GetCassandraSeeds(quoteIPs: true));
                                    text = text.Replace("$(vnetIP)", VNetIP);
                                    break;

                                case "cassandra-env.sh":

                                    text = text.Replace("$(maxMemMapCount)", string.Format("{0}", cassandraConfig.MaxMemMapCount));
                                    text = text.Replace("$(maxCassandraHeapSize)", string.Format("{0}M", cassandraConfig.MaxHeapSizeMB));
                                    text = text.Replace("$(maxCassandraNewHeapSize)", string.Format("{0}M", cassandraConfig.MaxNewHeapSizeMB));
                                    break;

                                case "opscenterd.conf":

                                    text = text.Replace("$(vnetIP)", VNetIP);
                                    break;

                                case "opscenter_cluster.conf":

                                    text = text.Replace("$(seedIPs)", GetCassandraSeeds());
                                    break;

                                case "cassandra.init.conf":
                                case "cassandra.initd.conf":

                                    text = text.Replace("$(maxMemMapCount)", cassandraConfig.MaxMemMapCount.ToString());
                                    break;
                            }

                            // We need to strip carriage returns from all text files and convert embedded TABs 
                            // to 4 spaces because YAML doesn't like tabs.

                            text = text.Replace("\r", string.Empty);
                            text = text.Replace("\t", "    ");

                            scpClient.Upload(new MemoryStream(Encoding.ASCII.GetBytes(text)), SetupFolder + file.Name);
                        }
                    }

                    if (Type == ServerType.CassandraNode)
                    {
                        if (cassandraConfig.WipeAndReconfig)
                        {
                            Status = "Wiping existing Cassandra services...";

                            RunCommand(sshClient, "sudo stop cassandra");

                            RunCommand(sshClient, "sudo rm {0}configured/cassandra", SetupFolder);

                            if (cassandraConfig.WipeData)
                            {
                                RunCommand(sshClient, "sudo rm -r /mnt-data/cassandra/*");
                                RunCommand(sshClient, "sudo rm -r /var/log/cassandra/*");
                            }
                        }

                        if (commonConfig.UpgradeLinux)
                        {
                            Status = "Linux upgrading...";

                            if (!RunCommand(sshClient, "sudo bash -x {0}upgrade-linux.sh", SetupFolder))
                            {
                                return false;
                            }
                        }

                        Status = "Server setup...";

                        if (!RunCommand(sshClient, "sudo bash -x {0}setup-cdb.sh", SetupFolder))
                        {
                            return false;
                        }

                        // NOTE:
                        //
                        // We don't want to reboot the VM here because the node will attempt to
                        // join the ring after it reboots and Cassandra does not support having
                        // multiple nodes join the ring simultaneously.
                        //
                        // We'll reboot the nodes one at a time in the cluster configuration method.
                    }
                    else if (Type == ServerType.CassandraOpsCenter)
                    {
                        // $todo(jeff.lill): OpsCenter wiping doesn't work, it leaves it in a weird state.

                        //if (cassandraConfig.WipeAndReconfig)
                        //{
                        //    Status = "Wiping existing Opscenter...";

                        //    RunCommand(sshClient, "sudo service opscenterd stop");

                        //    RunCommand(sshClient, "sudo rm {0}configured/opscenter", SetupFolder);
                        //    RunCommand(sshClient, "sudo rm -r /etc/opscenter/*");
                        //    RunCommand(sshClient, "sudo rm -r /var/log/opscenter/*");
                        //}

                        if (commonConfig.UpgradeLinux)
                        {
                            Status = "Linux upgrading...";

                            if (!RunCommand(sshClient, "sudo bash -x {0}upgrade-linux.sh", SetupFolder))
                            {
                                return false;
                            }
                        }

                        Status = "Server setup...";

                        if (!RunCommand(sshClient, "sudo bash -x {0}setup-cops.sh", SetupFolder))
                        {
                            return false;
                        }

                        // We're not going to start the service here.  Instead, we'll do that below
                        // after the node ring has been initialized.

                        Thread.Sleep(TimeSpan.FromSeconds(15));
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(10));         // Delay just to be safe
                }

                return true;
            }

            public bool ConfigureRedis()
            {
                using (var sshClient = ConnectSSh())
                {
                    if (!RunCommand(sshClient, "mkdir -p {0}", SetupFolder))
                    {
                        return false;
                    }

                    using (var scpClient = ConnectScp())
                    {
                        Status = "Uploading setup...";

                        var files = GetCommonFiles();

                        AddRedisFiles(files);

                        foreach (var file in files)
                        {
                            if (!file.IsEnabled)
                            {
                                continue;
                            }

                            var text = file.Contents;

                            // Replace macros in selected files.

                            switch (file.Name)
                            {
                                case "setup-hosts.sh":

                                    text = text.Replace("$(serverName)", Name);
                                    text = text.Replace("$(serviceDomain)", commonConfig.ServiceDomain);
                                    break;

                                case "setup-rds.sh":

                                    const string rdsSetupVars =
@"REDIS_VERSION={0}
LOGSTASH_VERSION={1}
RAID_CHUNK_SIZE_KB={2}
READ_AHEAD_SIZE_KB={3}
LOCAL_SSD={4}
";
                                    text = text.Replace("# @DEPLOYER-OVERRIDES@\r\n", string.Format(rdsSetupVars, redisConfig.RedisVersion, 
                                                                                                                  commonConfig.LogstashVersion, 
                                                                                                                  commonConfig.RaidChunkSizeKB, 
                                                                                                                  commonConfig.ReadAheadSizeKB,
                                                                                                                  LocalSSD ? "true" : "false"));
                                    break;

                                case "redis.conf":

                                    text = text.Replace("$(vnetIP)", VNetIP);
                                    text = text.Replace("$(clusterEnabled)", redisConfig.NodeCount == 1 ? "no" : "yes");
                                    text = text.Replace("$(clusterNodeTimeout)", ((int)redisConfig.ClusterNodeTimeout.TotalMilliseconds).ToString());
                                    text = text.Replace("$(maxMemoryMB)", ((int)redisConfig.MaxMemoryMB).ToString());
                                    break;
                            }

                            // We need to strip carriage returns from all text files and convert embedded TABs 
                            // to 4 spaces because YAML doesn't like tabs.

                            text = text.Replace("\r", string.Empty);
                            text = text.Replace("\t", "    ");

                            scpClient.Upload(new MemoryStream(Encoding.ASCII.GetBytes(text)), SetupFolder + file.Name);
                        }
                    }

                    if (commonConfig.UpgradeLinux)
                    {
                        Status = "Linux upgrading...";

                        if (!RunCommand(sshClient, "sudo bash -x {0}upgrade-linux.sh", SetupFolder))
                        {
                            return false;
                        }
                    }

                    Status = "Server setup...";

                    if (!RunCommand(sshClient, "sudo bash -x {0}setup-rds.sh", SetupFolder))
                    {
                        return false;
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(10));     // Delay just to be safe

                    Status = "Rebooting...";

                    if (!RunCommand(sshClient, "sudo reboot"))
                    {
                        return false;
                    }

                    WaitForBoot();

                    Thread.Sleep(TimeSpan.FromSeconds(15));     // Give REDIS a chance to start
                }

                return true;
            }

            public bool ConfigMongo()
            {
                string scriptName;

                if (mongoConfig.InstallTokuMX)
                {
                    scriptName = Type == ServerType.MongoData ? "setup-tdb.sh" : "setup-tqr.sh";
                }
                else
                {
                    scriptName = Type == ServerType.MongoData ? "setup-mdb.sh" : "setup-mqr.sh";
                }

                using (var sshClient = ConnectSSh())
                {
                    if (!RunCommand(sshClient, "mkdir -p {0}", SetupFolder))
                    {
                        return false;
                    }

                    using (var scpClient = ConnectScp())
                    {
                        Status = "Uploading setup...";

                        var files = GetCommonFiles();

                        AddMongoFiles(files);

                        foreach (var file in files)
                        {
                            if (!file.IsEnabled)
                            {
                                continue;
                            }

                            var text = file.Contents;

                            // Replace macros in selected files.

                            switch (file.Name)
                            {
                                case "setup-hosts.sh":

                                    text = text.Replace("$(serverName)", Name);
                                    text = text.Replace("$(serviceDomain)", commonConfig.ServiceDomain);
                                    break;

                                case "mongod.conf":
                                case "tokumxd.conf":

                                    text = text.Replace("$(journalEnabled)", mongoConfig.JournalEnabled ? "true" : "false");
                                    text = text.Replace("$(cacheSizeGB)", mongoConfig.CacheSizeGB.ToString());
                                    text = text.Replace("$(replSetName)", ReplicaSet);
                                    text = text.Replace("$(logVerbosity)", mongoConfig.LogVerbosity.ToString());
                                    text = text.Replace("$(directIO)", mongoConfig.TokuDirectIO ? "true" : "false");
                                    text = text.Replace("$(dataPort)", DataPort.ToString());
                                    break;

                                case "mongoc.conf":
                                case "tokumxc.conf":

                                    text = text.Replace("$(logVerbosity)", mongoConfig.LogVerbosity.ToString());
                                    break;

                                case "mongos.conf":
                                case "tokumxs.conf":

                                    text = text.Replace("$(shardChunkSizeMB)", mongoConfig.ShardChunkSizeMB.ToString());
                                    text = text.Replace("$(logVerbosity)", mongoConfig.LogVerbosity.ToString());
                                    text = text.Replace("$(configDBList)", GetConfigDbList());
                                    break;

                                case "upgrade-shards.sh":

                                    text = text.Replace("$(configDBList)", GetConfigDbList());
                                    break;

                                case "setup-mdb.sh":

                                    const string mdbSetupVars =
@"MONGO_VERSION={0}
LOGSTASH_VERSION={1}
RAID_CHUNK_SIZE_KB={2}
READ_AHEAD_SIZE_KB={3}
LOCAL_SSD={4}
";
                                    text = text.Replace("# @DEPLOYER-OVERRIDES@\r\n", string.Format(mdbSetupVars, mongoConfig.MongoVersion, 
                                                                                                                  commonConfig.LogstashVersion, 
                                                                                                                  commonConfig.RaidChunkSizeKB, 
                                                                                                                  commonConfig.ReadAheadSizeKB,
                                                                                                                  LocalSSD ? "true" : "false"));

                                    text = text.Replace("$(patchMongo)", mongoConfig.PatchMongo ? "true" : "false");
                                    text = text.Replace("$(patchMongoVersion)", mongoConfig.PatchMongoVersion);
                                    break;

                                case "setup-tdb.sh":

                                    const string tdbSetupVars =
@"TOKUMX_VERSION={0}
LOGSTASH_VERSION={1}
RAID_CHUNK_SIZE_KB={2}
READ_AHEAD_SIZE_KB={3}
LOCAL_SSD={4}
";
                                    text = text.Replace("# @DEPLOYER-OVERRIDES@\r\n", string.Format(tdbSetupVars, mongoConfig.TokuVersion,
                                                                                                                  commonConfig.LogstashVersion,
                                                                                                                  commonConfig.RaidChunkSizeKB,
                                                                                                                  commonConfig.ReadAheadSizeKB,
                                                                                                                  LocalSSD ? "true" : "false"));

                                    text = text.Replace("$(patchMongo)", mongoConfig.PatchMongo ? "true" : "false");
                                    text = text.Replace("$(patchMongoVersion)", mongoConfig.PatchMongoVersion);
                                    break;

                                case "setup-mqr.sh":

                                    const string mqrSetupVars =
@"MONGO_VERSION={0}
LOGSTASH_VERSION={1}
LOCAL_SSD={2}
";
                                    text = text.Replace("# @DEPLOYER-OVERRIDES@\r\n", string.Format(mqrSetupVars, mongoConfig.MongoVersion,
                                                                                                                  commonConfig.LogstashVersion,
                                                                                                                  LocalSSD ? "true" : "false"));

                                    text = text.Replace("$(patchMongo)", mongoConfig.PatchMongo ? "true" : "false");
                                    text = text.Replace("$(patchMongoVersion)", mongoConfig.PatchMongoVersion);
                                    break;

                                case "setup-tqr.sh":

                                    const string tqrSetupVars =
@"TOKUMX_VERSION={0}
LOGSTASH_VERSION={1}
LOCAL_SSD={2}
";
                                    text = text.Replace("# @DEPLOYER-OVERRIDES@\r\n", string.Format(tqrSetupVars, mongoConfig.TokuVersion, 
                                                                                                                  commonConfig.LogstashVersion,
                                                                                                                  LocalSSD ? "true" : "false"));
                                    break;

                                case "setup-mongo-admin.sh":
                                case "setup-toku-admin.sh":

                                    text = text.Replace("$(adminUser)", commonConfig.AdminUser);
                                    text = text.Replace("$(adminPassword)", commonConfig.AdminPassword);
                                    break;

                                case "setup-linux.sh":

                                    text = text.Replace("$(swapSizeMB)", GetSwapSizeMB(Type == ServerType.MongoRouter ? mongoConfig.RouterVmType : mongoConfig.DataVmType).ToString());
                                    break;
                            }

                            // We need to strip carriage returns from all text files and convert embedded TABs 
                            // to 4 spaces because YAML doesn't like tabs.

                            text = text.Replace("\r", string.Empty);
                            text = text.Replace("\t", "    ");

                            scpClient.Upload(new MemoryStream(Encoding.ASCII.GetBytes(text)), SetupFolder + file.Name);
                        }
                    }

                    if (mongoConfig.WipeAndReconfig)
                    {
                        Status = "Wiping existing MongoDB/TokuMX services...";

                        if (mongoConfig.InstallTokuMX)
                        {
                            RunCommand(sshClient, "sudo stop tokumxd");
                            RunCommand(sshClient, "sudo stop tokumxc");
                            RunCommand(sshClient, "sudo stop tokumxs");

                            RunCommand(sshClient, "sudo rm {0}configured/tokumxd", SetupFolder);
                            RunCommand(sshClient, "sudo rm {0}configured/tokumxs", SetupFolder);
                            RunCommand(sshClient, "sudo rm -r /mnt-data/tokumxc");
                            RunCommand(sshClient, "sudo rm -r /mnt-data/tokumxd");
                        }
                        else
                        {
                            RunCommand(sshClient, "sudo stop mongod");
                            RunCommand(sshClient, "sudo stop mongoc");
                            RunCommand(sshClient, "sudo stop mongos");

                            RunCommand(sshClient, "sudo rm {0}configured/mongod", SetupFolder);
                            RunCommand(sshClient, "sudo rm {0}configured/mongos", SetupFolder);
                            RunCommand(sshClient, "sudo rm -r /mnt-data/mongod");
                            RunCommand(sshClient, "sudo rm -r /mnt-data/mongoc");
                        }
                    }

                    if (commonConfig.UpgradeLinux)
                    {
                        Status = "Linux upgrading...";

                        if (!RunCommand(sshClient, "sudo bash -x {0}upgrade-linux.sh", SetupFolder))
                        {
                            return false;
                        }
                    }

                    Status = "Server setup...";

                    if (!RunCommand(sshClient, "sudo bash -x {0}{1}", SetupFolder, scriptName))
                    {
                        return false;
                    }

                    Status = "Mongo initializing...";

                    Thread.Sleep(TimeSpan.FromSeconds(15));     // Delay to give Mongo a chance to initialize

                    Status = "Stopping services...";

                    Thread.Sleep(TimeSpan.FromSeconds(15));

                    switch (Type)
                    {
                        case ServerType.MongoData:

                            if (mongoConfig.InstallTokuMX)
                            {
                                RunCommand(sshClient, "sudo stop tokumxd");
                                RunCommand(sshClient, "sudo stop tokumxc");
                            }
                            else
                            {
                                RunCommand(sshClient, "sudo stop mongod");
                                RunCommand(sshClient, "sudo stop mongoc");
                            }
                            break;

                        case ServerType.MongoRouter:

                            if (mongoConfig.InstallTokuMX)
                            {
                                RunCommand(sshClient, "sudo stop tokumxs");
                            }
                            else
                            {
                                RunCommand(sshClient, "sudo stop mongos");
                            }
                            break;
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(10));     // Delay just to be safe

                    Status = "Rebooting...";

                    if (!RunCommand(sshClient, "sudo reboot"))
                    {
                        return false;
                    }
                }

                return true;
            }

            public bool ConfigElastic()
            {
                var scriptName = Type == ServerType.ElasticData ? "setup-esd.sh" : "setup-esr.sh";

                using (var sshClient = ConnectSSh())
                {
                    if (!RunCommand(sshClient, "mkdir -p {0}", SetupFolder))
                    {
                        return false;
                    }

                    using (var scpClient = ConnectScp())
                    {
                        Status = "Uploading setup...";

                        var files = GetCommonFiles();

                        AddElasticFiles(files);

                        foreach (var file in files)
                        {
                            if (!file.IsEnabled)
                            {
                                continue;
                            }

                            var text            = file.Contents;
                            var elasticHttpPort = (Type == ServerType.ElasticRouter ? 9201 : 9200).ToString();

                            // Replace macros in selected files.

                            switch (file.Name)
                            {
                                case "setup-hosts.sh":

                                    text = text.Replace("$(serverName)", Name);
                                    text = text.Replace("$(serviceDomain)", commonConfig.ServiceDomain);
                                    break;

                                case "elasticsearch.yml":

                                    text = text.Replace("$(clusterName)", commonConfig.ServiceNamePrefix);
                                    text = text.Replace("$(shardCount)", elasticConfig.ShardCount.ToString());
                                    text = text.Replace("$(replicaCount)", elasticConfig.ReplicaCount.ToString());
                                    text = text.Replace("$(isDataVM)", Type == ServerType.ElasticData ? "true" : "false");
                                    text = text.Replace("$(vnetIP)", VNetIP);
                                    text = text.Replace("$(elasticHttpPort)", (Type == ServerType.ElasticRouter ? 9201 : 9200).ToString());

                                    var minMasterNodes   = Math.Max(1, elasticConfig.NodeCount / 2 + 1);
                                    var discoveryTimeout = (int)Math.Max(1.0, elasticConfig.DiscoveryTimeout.TotalSeconds);
                                    var discoverySeeds   = new StringBuilder();

                                    for (int i = 0; i < Math.Min(elasticConfig.NodeCount, elasticConfig.MaxDiscoverySeeds); i++)
                                    {
                                        if (i > 0)
                                        {
                                            discoverySeeds.Append(", ");
                                        }

                                        discoverySeeds.Append(servers.Single(s => s.Type == ServerType.ElasticData && s.NodeId == i).VNetIP);
                                    }

                                    text = text.Replace("$(minMasterNodes)", minMasterNodes.ToString());
                                    text = text.Replace("$(discoveryTimeout)", string.Format("{0}s", discoveryTimeout));
                                    text = text.Replace("$(discoverySeeds)", discoverySeeds.ToString());
                                    break;

                                case "logging.yml":

                                    break;

                                case "elasticsearch.init.conf":

                                    text = text.Replace("$(heapSizeMB)", elasticConfig.HeapSizeMB.ToString());
                                    text = text.Replace("$(maxMemMapCount)", elasticConfig.MaxMemMapCount.ToString());
                                    break;

                                case "kibana.yml":

                                    text = text.Replace("$(vnetIP)", VNetIP);
                                    text = text.Replace("$(elasticHttpPort)", elasticHttpPort);
                                    break;

                                case "nginx.conf":

                                    text = text.Replace("$(vnetIP)", VNetIP);
                                    text = text.Replace("$(elasticHttpPort)", elasticHttpPort);
                                    break;

                                case "setup-esd.sh":

                                    const string esdSetupVars =
@"ELASTIC_VERSION={0}
LOGSTASH_VERSION={1}
RAID_CHUNK_SIZE_KB={2}
READ_AHEAD_SIZE_KB={3}
LOCAL_SSD={4}
";
                                    text = text.Replace("# @DEPLOYER-OVERRIDES@\r\n", string.Format(esdSetupVars, elasticConfig.ElasticVersion, 
                                                                                                                  commonConfig.LogstashVersion,
                                                                                                                  commonConfig.RaidChunkSizeKB, 
                                                                                                                  commonConfig.ReadAheadSizeKB,
                                                                                                                  LocalSSD ? "true" : "false"));
                                    break;

                                case "setup-esr.sh":

                                    const string mqrSetupVars =
@"ELASTIC_VERSION={0}
KIBANA_VERSION={1}
NGINX_VERSION={2}
LOGSTASH_VERSION={3}
LOCAL_SSD={4}
";
                                    text = text.Replace("# @DEPLOYER-OVERRIDES@\r\n", string.Format(mqrSetupVars, elasticConfig.ElasticVersion, 
                                                                                                                  elasticConfig.KibanaVersion,
                                                                                                                  elasticConfig.NginxVersion,
                                                                                                                  commonConfig.LogstashVersion,
                                                                                                                  LocalSSD ? "true" : "false"));
                                    break;

                                case "setup-linux.sh":

                                    text = text.Replace("$(swapSizeMB)", GetSwapSizeMB(Type == ServerType.ElasticRouter ? elasticConfig.RouterVmType : elasticConfig.DataVmType).ToString());
                                    break;

                                case "setup-nginx-passwd.sh":

                                    text = text.Replace("$(adminUser)", commonConfig.AdminUser);
                                    text = text.Replace("$(adminPassword)", commonConfig.AdminPassword);
                                    text = text.Replace("$(kibanaUser)", elasticConfig.KibanaUser);
                                    text = text.Replace("$(kibanaPassword)", elasticConfig.KibanaPassword);
                                    text = text.Replace("$(elasticUser)", elasticConfig.ElasticUser);
                                    text = text.Replace("$(elasticPassword)", elasticConfig.ElasticPassword);
                                    break;
                            }

                            // We need to strip carriage returns from all text files and convert embedded TABs 
                            // to 4 spaces because YAML doesn't like tabs.

                            text = text.Replace("\r", string.Empty);
                            text = text.Replace("\t", "    ");

                            scpClient.Upload(new MemoryStream(Encoding.ASCII.GetBytes(text)), SetupFolder + file.Name);
                        }
                    }

                    if (commonConfig.UpgradeLinux)
                    {
                        Status = "Linux upgrading...";

                        if (!RunCommand(sshClient, "sudo bash -x {0}upgrade-linux.sh", SetupFolder))
                        {
                            return false;
                        }
                    }

                    Status = "Server setup...";

                    if (!RunCommand(sshClient, "sudo bash -x {0}{1}", SetupFolder, scriptName))
                    {
                        return false;
                    }

                    Status = "Configuring...";

                    Thread.Sleep(TimeSpan.FromSeconds(15));     // Delay to give Elasticsearch a chance to initialize

                    Status = "Rebooting...";

                    if (!RunCommand(sshClient, "sudo reboot"))
                    {
                        return false;
                    }
                }

                return true;
            }

            private string GetCassandraSeeds(bool quoteIPs = false)
            {
                var sb = new StringBuilder();

                // Add the VNET IP address for the first server in each rack as a seed.

                var rackConfigured = new HashSet<string>();

                foreach (var server in servers.Where(s => s.Type == ServerType.CassandraNode)
                                              .OrderBy(s => s.NodeId)
                                              .ThenBy(s => s.Rack))
                {
                    if (rackConfigured.Contains(server.Rack))
                    {
                        continue;
                    }

                    if (sb.Length > 0)
                    {
                        sb.Append(',');
                    }

                    if (quoteIPs)
                    {
                        sb.Append(string.Format("\"{0}\"", server.VNetIP));
                    }
                    else
                    {
                        sb.Append(server.VNetIP);
                    }


                    rackConfigured.Add(server.Rack);
                }

                return sb.ToString();
            }

            private void AssignRedisHashSlots(List<Server> masterNodes)
            {
                var slotsPerNode = 16384 / masterNodes.Count;
                var nextSlot     = 0;

                foreach (var server in masterNodes.OrderBy(s => s.NodeId))
                {
                    server.HashSlotFirst = nextSlot;
                    server.HashSlotLast  = nextSlot + slotsPerNode;

                    nextSlot += slotsPerNode + 1;
                }

                masterNodes.Last().HashSlotLast = 16383;
            }

            private string RedisClusterConfigScript()
            {
                var sb        = new StringBuilder();
                var firstNode = servers.Where(s => s.NodeId == 0).Single();

                sb.AppendLine("#------------------------------------------------------------------------------");
                sb.AppendLine("# setup-cluster.sh");
                sb.AppendLine("#");
                sb.AppendLine("# This script is custom generated by the deployer to initialize the REDIS cluster.");
                sb.AppendLine("# The deployer will run this script on the first REDIS cluster node only.");
                sb.AppendLine();
                sb.AppendFormat("SETUP_DIR={0}\r\n", SetupFolder.Substring(0, SetupFolder.Length - 1));
                sb.AppendLine();

                if (redisConfig.NodeCount <= 1)
                {
                    sb.AppendLine("# Stand-alone cluster: No cluster configuration required.");
                }
                else
                {
                    if (redisConfig.Replicate)
                    {
                        sb.AppendLine("# Replicated cluster:");
                        sb.AppendLine();
                        sb.AppendLine("# Introduce all cluster nodes to the first cluster node.");
                        sb.AppendLine();

                        foreach (var server in servers.Where(s => s.NodeId != 0))
                        {
                            sb.AppendFormat("redis-cli -h {0} cluster meet {1} 6379\r\n", firstNode.VNetIP, server.VNetIP);
                        }

                        sb.AppendLine();
                        sb.AppendLine("# Sleep for a few seconds to ensure that the cluster has stabilized.");
                        sb.AppendLine();
                        sb.AppendLine("sleep 30");

                        sb.AppendLine();
                        sb.AppendLine("# List the cluster nodes so we can extract the REDIS cluster node IDs for");
                        sb.AppendLine("# each MASTER node below when we assign its SLAVE.");
                        sb.AppendLine();
                        sb.AppendFormat("redis-cli -h {0} cluster nodes > $SETUP_DIR/cluster.nodes\r\n", firstNode.VNetIP);

                        sb.AppendLine();
                        sb.AppendLine("# Associate a SLAVE node with each MASTER.  We'll do this such that a MASTER");
                        sb.AppendLine("# with IP address [10.0.0.X] will be associated with the slave with IP address");
                        sb.AppendLine("# [10.0.0.X+1].");

                        foreach (var slave in servers.Where(s => IsOdd(s.NodeId)))
                        {
                            sb.AppendLine();

                            // Associate MASTER with NodeId==(slaveId-1) with the SLAVE.

                            var master = servers.Single(s => s.NodeId == slave.NodeId - 1);

                            sb.AppendFormat("masterId=$(grep {0}:6379 $SETUP_DIR/cluster.nodes | cut -d' ' -f1)\r\n", master.VNetIP);   // Extract the cluster ID for the master
                            sb.AppendFormat("redis-cli -h {0} cluster replicate $masterId\r\n", slave.VNetIP);                          // Assign the slave to the master
                        }

                        sb.AppendLine();
                        sb.AppendLine("# Assign hash slots to each MASTER.");
                        sb.AppendLine();

                        var masterNodes = servers.Where(s => IsEven(s.NodeId)).ToList();

                        AssignRedisHashSlots(masterNodes);

                        foreach (var server in masterNodes)
                        {
                            sb.AppendFormat("for (( slot={0}; slot<={1}; slot++ ))\r\n", server.HashSlotFirst, server.HashSlotLast);
                            sb.AppendLine("do");
                            sb.AppendFormat("    redis-cli -h {0} cluster addslots $slot\r\n", server.VNetIP);
                            sb.AppendLine("done");

                            sb.AppendLine();
                        }
                    }
                    else
                    {
                        sb.AppendLine("# Non-replicated cluster: Configure all nodes as MASTER.");
                        sb.AppendLine();

                        foreach (var server in servers)
                        {
                            sb.AppendFormat("redis-cli -h {0} cluster meet {1} 6379\r\n", firstNode.VNetIP, server.VNetIP);
                        }

                        sb.AppendLine();
                        sb.AppendLine("# Sleep for a few seconds to ensure that the cluster has stabilized.");
                        sb.AppendLine();
                        sb.AppendLine("sleep 30");

                        sb.AppendLine();
                        sb.AppendLine("# Assign hash slots to each node.");
                        sb.AppendLine();

                        var masterNodes = servers;

                        AssignRedisHashSlots(masterNodes);

                        foreach (var server in masterNodes)
                        {
                            sb.AppendFormat("for (( slot={0}; slot<={1}; slot++ ))\r\n", server.HashSlotFirst, server.HashSlotLast);
                            sb.AppendLine("do");
                            sb.AppendFormat("    redis-cli -h {0} cluster addslots $slot\r\n", server.VNetIP);
                            sb.AppendLine("done");

                            sb.AppendLine();
                        }
                    }

                    sb.AppendLine("# Sleep for a few seconds to ensure that the cluster has stabilized.");
                    sb.AppendLine();
                    sb.AppendLine("sleep 30");
                }

                return sb.ToString();
            }

            private string MongoShardConfigScript()
            {
                if (Type != ServerType.MongoData)
                {
                    return string.Empty;
                }

                var replicaConfig = new BsonDocument("_id", GetMongoShardName(ShardId));
                var members       = new BsonArray();

                for (int replicaId = 0; replicaId < mongoConfig.ReplicaCount; replicaId++)
                {
                    members.Add(
                        new BsonDocument(
                            new List<BsonElement>()
                                {
                                    new BsonElement("_id", replicaId ),
                                    new BsonElement("host", string.Format("{0}:{1}", Host, GetMongoDataPort(ShardId, replicaId))),
                                    new BsonElement("priority", replicaId == 0 ? 10 : 5)    // Replica[0] will be the primary by default
                                }
                        )
                    );
                }

                replicaConfig.Add("members", members);

                var replicaCommand = new BsonDocument("replSetInitiate", replicaConfig);

                return string.Format(
@"#------------------------------------------------------------------------------
# Initializes a shard's replica set.  Run this on the first replica for the shard.

mongo localhost:{0} << EOF
use admin
db.runCommand( {1} )
EOF
",
 DataPort,
 replicaCommand);
            }

            private string MongoRouterConfigScript()
            {
                if (Type != ServerType.MongoRouter)
                {
                    return string.Empty;
                }

                var dataHost = GetMongoDataHost();
                var sb       = new StringBuilder();

                sb.AppendFormat(
@"#------------------------------------------------------------------------------
# Initializes the sharded cluster.  Run this on one of the router instances.

mongo localhost:27017 << EOF
use admin
db.auth(""{0}"", ""{1}"")
",
 commonConfig.AdminUser,
 commonConfig.AdminPassword);

                for (var shardId = 0; shardId < mongoConfig.ShardCount; shardId++)
                {
                    sb.AppendFormat("db.runCommand( {{ addShard : \"{0}/{1}:{2}\" }} )\r\n", GetMongoShardName(shardId), dataHost, GetMongoDataPort(shardId, 0));
                }

                sb.AppendLine("EOF");

                return sb.ToString();
            }
        }

        public static void InitializeLogs()
        {
            logFolder = Path.Combine(Directory.GetCurrentDirectory(), "Setup-Logs");

            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }

            foreach (var logFilePath in Directory.GetFiles(logFolder, "*.log"))
            {
                File.Delete(logFilePath);
            }
        }

        public static void InitializeMongoServers()
        {
            // Initialize the server state.

            for (int routerId = 0; routerId < mongoConfig.RouterCount; routerId++)
            {
                servers.Add(
                    new Server(mongoConfig.RouterVmType)
                    {
                        Name       = string.Format("{0}-mqr-{1}", commonConfig.ServiceNamePrefix, routerId),
                        Host       = string.Format("{0}-mqr.{1}", commonConfig.ServiceNamePrefix, commonConfig.ServiceDomain),
                        Type       = ServerType.MongoRouter,
                        Status     = "Waiting for VM",
                        SshPort    = GetSshPort(routerId),
                        DataPort   = GetMongoDataPort(routerId),
                        ConfPort   = 0,
                        ReplicaSet = string.Empty,
                        RouterId   = routerId
                    });
            }

            for (int shardId = 0; shardId < mongoConfig.ShardCount; shardId++)
            {
                for (int replicaId = 0; replicaId < mongoConfig.ReplicaCount; replicaId++)
                {
                    var serverName = string.Format("{0}-mdb-{1}{2}", commonConfig.ServiceNamePrefix, ((char)('a' + shardId)).ToString(), replicaId);

                    servers.Add(
                        new Server(mongoConfig.DataVmType)
                        {
                            Name       = serverName,
                            Host       = commonConfig.PortMapping ? string.Format("{0}-mdb.{1}", commonConfig.ServiceNamePrefix, commonConfig.ServiceDomain) 
                                                                  : $"{serverName}.{commonConfig.ServiceDomain}",
                            Type       = ServerType.MongoData,
                            Status     = "Waiting for VM",
                            SshPort    = GetMongoSshPort(shardId, replicaId),
                            DataPort   = GetMongoDataPort(shardId, replicaId),
                            ConfPort   = GetMongoConfPort(shardId, replicaId),
                            ReplicaSet = GetMongoShardName(shardId),
                            ShardId    = shardId,
                            ReplicaId  = replicaId
                        });
                }
            }
        }

        public static void InitializeElasticServers()
        {
            // Initialize the server state.

            for (int routerId = 0; routerId < elasticConfig.RouterCount; routerId++)
            {
                var serverName = string.Format("{0}-esr-{1}", commonConfig.ServiceNamePrefix, routerId);
                var serverHost = string.Format("{0}.{1}", serverName, commonConfig.ServiceDomain);
                var vnetIP     = string.Empty;

                if (commonConfig.Azure)
                {
                    // Determine the local IP address of the VM within the Azure VNET.  Note that
                    // router nodes are located within the 10.0.0.0/24 subnet and the data
                    // nodes are in the 10.0.1.0/24 subnet and that Azure reserves the first
                    // four address in each subnet, so the first node's address in each
                    // subnet will be 10.0.x.4.

                    vnetIP = string.Format("10.0.0.{0}", routerId + 4);
                }
                else
                {
                    // Use the node's DNS IP address as the local IP.

                    vnetIP = Dns.GetHostAddresses(serverHost)[0].ToString();
                }

                servers.Add(
                    new Server(elasticConfig.RouterVmType)
                    {
                        Name       = serverName,
                        Host       = commonConfig.PortMapping ? string.Format("{0}-esr.{1}", commonConfig.ServiceNamePrefix, commonConfig.ServiceDomain) 
                                                              : serverHost,
                        Type       = ServerType.ElasticRouter,
                        Status     = "Waiting for VM",
                        VNetIP     = vnetIP,
                        SshPort    = GetSshPort(routerId),
                        DataPort   = GetElasticDataPort(routerId),
                        ConfPort   = 0,
                        ReplicaSet = string.Empty,
                        RouterId   = routerId
                    });
            }

            for (int nodeId = 0; nodeId < elasticConfig.NodeCount; nodeId++)
            {
                var serverName = string.Format("{0}-esd-{1}", commonConfig.ServiceNamePrefix, nodeId);
                var serverHost = string.Format("{0}.{1}", serverName, commonConfig.ServiceDomain);
                var vnetIP     = string.Empty;

                if (commonConfig.Azure)
                {
                    // Determine the local IP address of the VM within the Azure VNET.  Note that
                    // router nodes are located within the 10.0.0.0/24 subnet and the data
                    // nodes are in the 10.0.1.0/24 subnet and that Azure reserves the first
                    // four address in each subnet, so the first node's address in each
                    // subnet will be 10.0.x.4.

                    vnetIP = string.Format("10.0.1.{0}", nodeId + 4);
                }
                else
                {
                    // Use the node's DNS IP address as the local IP.

                    vnetIP = Dns.GetHostAddresses(serverHost)[0].ToString();
                }

                servers.Add(
                    new Server(elasticConfig.DataVmType)
                    {
                        Datacenter = commonConfig.Datacenter,
                        Name       = serverName,
                        Host       = commonConfig.PortMapping ? string.Format("{0}-esd.{1}", commonConfig.ServiceNamePrefix, commonConfig.ServiceDomain)
                                                              : serverHost,
                        Type       = ServerType.ElasticData,
                        Status     = "Waiting for VM",
                        VNetIP     = vnetIP,
                        SshPort    = GetSshPort(nodeId),
                        DataPort   = GetElasticDataPort(nodeId),
                        NodeId     = nodeId,
                        Rack       = string.Format("Rack-{0}", nodeId % cassandraConfig.RackCount)
                    });
            }
        }

        public static void InitializeCassandraServers()
        {
            // Initialize the server state.

            if (cassandraConfig.EnableOpscenter)
            {
                var serverName = string.Format("{0}-cops", commonConfig.ServiceNamePrefix);
                var serverHost = string.Format("{0}.{1}", serverName, commonConfig.ServiceDomain);

                servers.Add(
                    new Server(cassandraConfig.OpscenterVmType)
                    {
                        Datacenter = commonConfig.Datacenter,
                        Name       = serverName,
                        Host       = serverHost,
                        Type       = ServerType.CassandraOpsCenter,
                        Status     = "Waiting for VM",
                        SshPort    = commonConfig.PortMapping ? 22253 : 22,
                        VNetIP     = commonConfig.PortMapping ? string.Format("10.0.0.{0}", 253) : Dns.GetHostAddresses(serverHost)[0].ToString(),
                });
            }

            for (int nodeId = 0; nodeId < cassandraConfig.NodeCount; nodeId++)
            {
                var serverName = string.Format("{0}-cdb-{1}", commonConfig.ServiceNamePrefix, nodeId);
                var serverHost = string.Format("{0}.{1}", serverName, commonConfig.ServiceDomain);

                servers.Add(
                    new Server(cassandraConfig.DataVmType)
                    {
                        Datacenter = commonConfig.Datacenter,
                        Name       = serverName,
                        Host       = commonConfig.PortMapping ? string.Format("{0}-cdb.{1}", commonConfig.ServiceNamePrefix, commonConfig.ServiceDomain)
                                                              : serverHost,
                        Type       = ServerType.CassandraNode,
                        Status     = "Waiting for VM",
                        SshPort    = GetSshPort(nodeId),
                        DataPort   = GetCassandraDataPort(nodeId),
                        NodeId     = nodeId,
                        VNetIP     = commonConfig.PortMapping ? string.Format("10.0.0.{0}", nodeId + 4) : Dns.GetHostAddresses(serverHost)[0].ToString(),
                        Rack       = string.Format("Rack-{0}", nodeId % cassandraConfig.RackCount)
                    });
            }
        }

        public static void DeployCassandra()
        {
            // Initialization.

            InitializeLogs();
            InitializeCassandraServers();

            // Compute the JVM heap size parameters.

            var dataVmInfo    = VmInfo.GetVmInfo(cassandraConfig.DataVmType);
            var vmRamGB       = dataVmInfo.RamGB;
            var maxHeapSizeGB = (int)(Math.Max(1.0, vmRamGB - cassandraConfig.FreeMemoryGB));

            cassandraConfig.MaxNewHeapSizeMB = dataVmInfo.Cores * 100;

            // Compute the Cassandra JAVA heap size.  We're currently using the same
            // algorithm from the [cassandra-env.sh] file installed with Cassandra:
            //
            //      set max heap size based on the following:
            //
            //      max(min(1/2 ram, 1024MB), min(1/4 ram, 8GB))
            //      calculate 1/2 ram and cap to 1024MB
            //      calculate 1/4 ram and cap to 8192MB
            //      pick the max

            cassandraConfig.MaxHeapSizeMB = 1024 * (int)Math.Max(Math.Min(vmRamGB/2, 1.0), Math.Min(vmRamGB/4, 8));

            // $todo(jeff.lill): Need to refine this:
            //
            // Compute the size of the off heap row cache.  We're going to reserve 8GB for the OS and disk cache,
            // subtract off the JAVA heap computed above and then use the remaining RAM as row cache.

            cassandraConfig.RowCacheSizeMB = (int)Math.Max(0, vmRamGB * 1024 - (8192 + cassandraConfig.MaxNewHeapSizeMB));
            cassandraConfig.RowCacheSizeMB = 0;

            // Provision each VM in a separate thread.

            foreach (var server in servers)
            {
                var thread = new Thread(new ThreadStart(
                    () => 
                    {
                        server.WaitForBoot();

                        if (!server.ConfigureCassandra())
                        {
                            return;
                        }

                        if (server.Type == ServerType.CassandraNode)
                        {
                            server.Status = "Join pending";
                        }
                        else
                        {
                            server.Status = "Waiting for cluster";
                        }

                        server.IsReady = true;
                    }));

                thread.Start();
            }

            DisplayCassandraStatus(waitForReady: true);

            // Cassandra does not support having multiple nodes join the ring simultaneously.
            // We need to add the nodes rto the ring one at a time and then allow two minutes
            // for the cluster topology to settle down before joining the next node.
            //
            // Note that the it's important that the first node added be one of the seed nodes.
            // Other code here ensures that the first node is a seed node.

            foreach (var server in servers.Where(s => s.Type == ServerType.CassandraNode))
            {
                server.IsReady = false;
            }

            var joinThread = new Thread(new ThreadStart(
                () =>
                {
                    foreach (var server in servers.Where(s => s.Type == ServerType.CassandraNode))
                    {
                        server.Status = "Rebooting...";

                        using (var sshClient = server.ConnectSSh())
                        {
                            if (!server.RunCommand(sshClient, "sudo reboot"))
                            {
                                break;
                            }
                        }

                        server.WaitForBoot();

                        server.Status = "Joining cluster...";

                        Thread.Sleep(cassandraConfig.ClusterJoinDelay);

                        server.Status  = "*** Joined ***";
                        server.IsReady = true;
                    }

                }));

            joinThread.Start();

            DisplayCassandraStatus(waitForReady: true);

            if (cassandraConfig.EnableOpscenter)
            {
                var opsThread = new Thread(new ThreadStart(
                    () =>
                    {
                        var server = servers.Where(s => s.Type == ServerType.CassandraOpsCenter).Single();

                        server.Status = "Rebooting...";

                        using (var sshClient = server.ConnectSSh())
                        {
                            server.RunCommand(sshClient, "sudo reboot");
                        }

                        server.WaitForBoot();

                        server.Status  = "*** Started ***";
                        server.IsReady = true;
                    }));

                opsThread.Start();

                DisplayCassandraStatus(waitForReady: true);
            }

            Console.WriteLine();
            Console.WriteLine("*** Cluster Provisioned ***");
        }

        public static void InitializeRedisServers()
        {
            // Initialize the server state.

            for (int nodeId = 0; nodeId < redisConfig.NodeCount; nodeId++)
            {
                var serverName = string.Format("{0}-rds-{1}", commonConfig.ServiceNamePrefix, nodeId);
                var serverHost = string.Format("{0}.{1}", serverName, commonConfig.ServiceDomain);
                var vnetIP     = commonConfig.PortMapping ? string.Format("10.0.0.{0}", nodeId + 4)
                                                          : Dns.GetHostAddresses(serverHost)[0].ToString();

                servers.Add(
                    new Server(redisConfig.DataVmType)
                    {
                        Name    = serverName,
                        Host    = commonConfig.PortMapping ? string.Format("{0}-rds.{1}", commonConfig.ServiceNamePrefix, commonConfig.ServiceDomain)
                                                           : serverHost,
                        Type    = ServerType.Redis,
                        Status  = "Waiting for VM",
                        SshPort = GetSshPort(nodeId),
                        NodeId  = nodeId,
                        Rack    = string.Format("Rack-{0}", IsEven(nodeId) ? 0 : 1),
                        VNetIP  = vnetIP,
                    });
            }
        }

        public static void DeployRedis()
        {
            // Initialization.

            InitializeLogs();
            InitializeRedisServers();

            // Calculate the computed settings.

            var dataVmInfo = VmInfo.GetVmInfo(redisConfig.DataVmType);
            var vmRamGB    = dataVmInfo.RamGB;

            // Linux + Redis consumes about 800MB with no data.  We'll reserve 1.5GB
            // for this and system services and leave the rest for Redis data.

            redisConfig.MaxMemoryMB = (int)((vmRamGB - 1.5) * 1024);

            // Provision each VM in a separate thread.

            foreach (var server in servers)
            {
                new Thread(new ThreadStart(
                    () => 
                    {
                        server.WaitForBoot();

                        if (!server.ConfigureRedis())
                        {
                            return;
                        }

                        server.IsReady = true;
                        server.Status  = "Join pending";

                    })).Start();
            }

            DisplayRedisStatus(waitForReady: true);

            // Initialize the cluster by calling the [setup-cluster.sh] script on the
            // first cluster node.

            var firstNode = servers.Where(s => s.NodeId == 0).Single();

            firstNode.IsReady = false;
            firstNode.Status  = "Configuring cluster...";

            new Thread(new ThreadStart(
                () => 
                {
                    using (var sshClient = firstNode.ConnectSSh())
                    {
                        firstNode.RunCommand(sshClient, "bash -x {0}setup-cluster.sh", firstNode.SetupFolder);

                        foreach (var server in servers)
                        {
                            server.Status = "*** Ready ***";
                        }

                        firstNode.IsReady = true;
                    }

                })).Start();

            DisplayRedisStatus(waitForReady: true);

            Console.WriteLine();
            Console.WriteLine("*** Cluster Provisioned ***");
        }

        public static void DeployMongo()
        {
            // Initialization.

            InitializeLogs();
            InitializeMongoServers();

            // Compute the Mongo cache size based on the RAM available on
            // the data VM.

            var dataVmInfo = VmInfo.GetVmInfo(mongoConfig.DataVmType);

            mongoConfig.CacheSizeGB = (int)(Math.Max(1.0, dataVmInfo.RamGB - mongoConfig.FreeMemoryGB));    // Leave some RAM for the OS and disk cache

            // Generate a pseudo random cluster key to be be used by MONGOS
            // routers, MONGOC instances and MONGOD replica set members to
            // authenticate each other.  This key will be installed on all
            // servers.

            var clusterKeyBytes = new byte[512];

            new Random((int) DateTime.Now.Ticks).NextBytes(clusterKeyBytes);

            mongoConfig.ClusterKey = Convert.ToBase64String(clusterKeyBytes);
            mongoConfig.ClusterKey = mongoConfig.ClusterKey.Replace("=", string.Empty);     // Mongo doesn't like the '=' filler characters

            // Provision each VM in a separate thread.

            foreach (var server in servers)
            {
                var thread = new Thread(new ThreadStart(
                    () => 
                    {
                        server.WaitForBoot();

                        if (!server.ConfigMongo())
                        {
                            return;
                        }

                        server.WaitForBoot();

                        if (server.Type == ServerType.MongoData)
                        {
                            server.WaitForMongod();
                            server.WaitForMongoc();

                            server.Status = "Replica config pending";
                        }
                        else
                        {
                            server.Status = "Router config pending";
                        }

                        server.IsReady = true;
                    }));

                thread.Start();
            }

            DisplayMongoStatus(waitForReady: true);

            // Configure the Mongo cluster in four steps.

            // STEP 1: Configure the shard replica sets.

            foreach (var server in servers)
            {
                if (server.Type == ServerType.MongoData && server.ReplicaId == 0)
                {
                    server.IsReady = false;
                    server.Status  = "Configuring shards...";
                }
            }

            DisplayMongoStatus();

            foreach (var server in servers.Where(s => !s.IsReady))
            {
                var thread = new Thread(new ThreadStart(
                    () =>
                    {
                        using (var sshClient = server.ConnectSSh())
                        {
                            if (!server.RunCommand(sshClient, "bash -x {0}setup-shard.sh", server.SetupFolder))
                            {
                                return;
                            }

                            // STEP 2: Wait until the server becomes the master and then
                            //         create the ROOT admin user.  Note thatmong this is a bit
                            //         fragile since the election delay is hardcoded and I'm 
                            //         assuming that the first replica will be elected as primary.

                            server.Status = "Replica set initializing...";
                            Thread.Sleep(TimeSpan.FromSeconds(60));

                            server.Status = "Creating replica ADMIN user...";

                            var setupAdminScript = mongoConfig.InstallTokuMX ? "setup-toku-admin.sh" : "setup-mongo-admin.sh";

                            if (!server.RunCommand(sshClient, "bash -x {0}{1} {2}", server.SetupFolder, setupAdminScript, server.DataPort))
                            {
                                return;
                            }
                        }

                        server.Status  = "Replica configured";
                        server.IsReady = true;
                    }));

                thread.Start();
            }

            DisplayMongoStatus(waitForReady: true);

            foreach (var server in servers.Where(s => s.Type == ServerType.MongoData))
            {
                server.Status = "Replica set ready";
            }

            // STEP 3: Wait for MONGOS to start on each router.  Note that
            //         MONGOS won't start until is able to contact all of
            //         the CONFIG servers.  The [service-starter.sh] CRON
            //         job is responsible for trying to restart MONGOS
            //         on 1 minute intervals.

            foreach (var server in servers.Where(s => s.Type == ServerType.MongoRouter))
            {
                server.IsReady = false;
                server.Status  = "Waiting for MONGOS";

                var thread = new Thread(new ThreadStart(
                    () =>
                    {
                        server.WaitForMongos();

                        server.Status  = "MONGOS ready";
                        server.IsReady = true;
                    }));

                thread.Start();
            }

            DisplayMongoStatus(waitForReady: true);

            // STEP 4: Configure the routers

            var router = servers.FirstOrDefault(s => s.Type == ServerType.MongoRouter);

            if (router != null)
            {
                router.IsReady = false;
                router.Status  = "Configuring routers...";
            }

            DisplayMongoStatus();

            if (router != null)
            {
                var thread = new Thread(new ThreadStart(
                    () =>
                    {
                        using (var sshClient = router.ConnectSSh())
                        {
                            router.WaitForMongos();

                            router.Status = "Creating cluster ADMIN user...";

                            var setupAdminScript = mongoConfig.InstallTokuMX ? "setup-toku-admin.sh" : "setup-mongo-admin.sh";

                            if (!router.RunCommand(sshClient, "bash -x {0}{1} 27017", router.SetupFolder, setupAdminScript))
                            {
                                return;
                            }

                            router.Status = "Configuring routers...";

                            if (!router.RunCommand(sshClient, "bash -x {0}setup-router.sh 27017", router.SetupFolder))
                            {
                                return;
                            }
                        }

                        router.Status  = "Routers ready";
                        router.IsReady = true;
                    }));

                thread.Start();
            }

            DisplayMongoStatus(waitForReady: true);

            // Run the cleanup script on all servers.

            foreach (var server in servers)
            {
                server.Status  = "Cleanup...";
                server.IsReady = false;

                var thread = new Thread(new ThreadStart(
                    () =>
                    {
                        using (var sshClient = server.ConnectSSh())
                        {
                            if (!server.RunCommand(sshClient, "sudo bash -x {0}setup-clean.sh {0}", server.SetupFolder))
                            {
                                return;
                            }
                        }

                        server.Status  = "Cleaned";
                        server.IsReady = true;
                    }));

                thread.Start();
            }

            DisplayMongoStatus(waitForReady: true);

            foreach (var server in servers)
            {
                server.Status = "*** Ready ***";
            }

            DisplayMongoStatus(waitForReady: true);

            Console.WriteLine();
            Console.WriteLine("*** Cluster Provisioned ***");
        }

        public static void DeployElastic()
        {
            // Initialization.

            InitializeLogs();
            InitializeElasticServers();

            // Compute the fixed size of the Java heap to be allocated for Elasticsearch.
            // The heap will be preallocated on service start and will be locked into
            // memory to prevent swapping.
            //
            // Elasticsearch recommends that half of system RAM, up to a maximum of
            // 31GB, be allocated to the heap with the remaining RAM being available
            // for the disk cache and the Lucene indexes.  Visit the following link
            // for more information:
            //
            // https://www.elastic.co/guide/en/elasticsearch/guide/current/heap-sizing.html

            var ramMB = VmInfo.GetVmInfo(elasticConfig.DataVmType).RamGB * 1024;

            elasticConfig.HeapSizeMB = (int)Math.Min(31 * 1024, ramMB/2);

            // Provision each VM in a separate thread.

            foreach (var server in servers)
            {
                var thread = new Thread(new ThreadStart(
                    () => 
                    {
                        server.WaitForBoot();

                        if (!server.ConfigElastic())
                        {
                            return;
                        }

                        server.WaitForBoot();

                        server.IsReady = true;
                    }));

                thread.Start();
            }

            DisplayElasticStatus(waitForReady: true);

            Console.WriteLine();
            Console.WriteLine("*** Cluster Provisioned ***");
        }

        public static void RunCommands(Action initAction, Action displayAction, Func<Server, bool> commandExecuter)
        {
            // Initialization.

            InitializeLogs();
            initAction();

            foreach (var server in servers)
            {
                var thread = new Thread(new ThreadStart(
                    () =>
                    {
                        server.Status  = "Waiting for VM";
                        server.IsReady = false;

                        server.WaitForBoot();

                        if (commandExecuter(server))
                        {
                            server.Status = "Rebooting...";

                            using (var sshClient = server.ConnectSSh())
                            {
                                server.RunCommand(sshClient, "sudo reboot");
                                Thread.Sleep(TimeSpan.FromSeconds(10));
                            }
                        }

                        server.WaitForBoot();

                        server.Status  = "*** Ready ***";
                        server.IsReady = true;
                    }));

                thread.Start();
            }

            displayAction();

            Console.WriteLine();
            Console.WriteLine("*** Cluster Ready ***");
        }

        public static void DisplayMongoStatus(bool waitForReady = false)
        {
            if (waitForReady)
            {
                while (true)
                {
                    DisplayMongoStatus();

                    Thread.Sleep(TimeSpan.FromSeconds(2));

                    if (!servers.Exists(s => !s.IsReady))
                    {
                        break;
                    }
                }

                DisplayMongoStatus();
            }
            else
            {
                Console.Clear();
                Console.WriteLine("Mongo Query Routers");
                Console.WriteLine("-------------------");

                foreach (var server in servers.Where(s => s.Type == ServerType.MongoRouter))
                {
                    Console.WriteLine("{0,-20}{1}", server.Name + ": ", server.Status);
                }

                Console.WriteLine();
                Console.WriteLine();

                Console.WriteLine("Mongo Data Servers");
                Console.WriteLine("------------------");

                foreach (var server in servers.Where(s => s.Type == ServerType.MongoData))
                {
                    if (server.ShardId > 0 && server.ReplicaId == 0)
                    {
                        Console.WriteLine();
                    }

                    Console.WriteLine("{0,-20}{1}", server.Name + ": ", server.Status);
                }
            }
        }

        public static void DisplayCassandraStatus(bool waitForReady = false)
        {
            if (waitForReady)
            {
                while (true)
                {
                    DisplayCassandraStatus();

                    Thread.Sleep(TimeSpan.FromSeconds(2));

                    if (!servers.Exists(s => !s.IsReady))
                    {
                        break;
                    }
                }

                DisplayCassandraStatus();
            }
            else
            {
                Console.Clear();

                if (cassandraConfig.EnableOpscenter)
                {
                    Console.WriteLine("Cassandra OpsCenter");
                    Console.WriteLine("-------------------");

                    foreach (var server in servers.Where(s => s.Type == ServerType.CassandraOpsCenter))
                    {
                        Console.WriteLine("{0,-20}{1}", server.Name + ": ", server.Status);
                    }

                    Console.WriteLine();
                }

                Console.WriteLine();
                Console.WriteLine("Cassandra Nodes");
                Console.WriteLine("---------------");

                foreach (var server in servers.Where(s => s.Type == ServerType.CassandraNode))
                {
                    Console.WriteLine("{0,-20}{1}", server.Name + ": ", server.Status);
                }
            }
        }

        public static void DisplayRedisStatus(bool waitForReady = false)
        {
            if (waitForReady)
            {
                while (true)
                {
                    DisplayRedisStatus();

                    Thread.Sleep(TimeSpan.FromSeconds(2));

                    if (!servers.Exists(s => !s.IsReady))
                    {
                        break;
                    }
                }

                DisplayRedisStatus();
            }
            else
            {
                Console.Clear();


                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Redis Nodes");
                Console.WriteLine("-----------");

                foreach (var server in servers)
                {
                    Console.WriteLine("{0,-20}{1}", server.Name + ": ", server.Status);
                }
            }
        }

        public static void DisplayElasticStatus(bool waitForReady = false)
        {
            if (waitForReady)
            {
                while (true)
                {
                    DisplayElasticStatus();

                    Thread.Sleep(TimeSpan.FromSeconds(2));

                    if (!servers.Exists(s => !s.IsReady))
                    {
                        break;
                    }
                }

                DisplayElasticStatus();
            }
            else
            {
                Console.Clear();
                Console.WriteLine("Elastic Query Routers");
                Console.WriteLine("---------------------");

                foreach (var server in servers.Where(s => s.Type == ServerType.ElasticRouter))
                {
                    Console.WriteLine("{0,-20}{1}", server.Name + ": ", server.Status);
                }

                Console.WriteLine();
                Console.WriteLine();

                Console.WriteLine("Elastic Data Nodes");
                Console.WriteLine("------------------");

                foreach (var server in servers.Where(s => s.Type == ServerType.ElasticData))
                {
                    Console.WriteLine("{0,-20}{1}", server.Name + ": ", server.Status);
                }
            }
        }

        public static int GetSwapSizeMB(string azureVMType)
        {
            // We're going to allocate a swapfile large enough for 1/4 or RAM.

            return (int)(1024 * VmInfo.GetVmInfo(azureVMType).RamGB / 4);
        }

        public static string GetMongoRouterHost()
        {
            return string.Format("{0}-mqr.{1}", commonConfig.ServiceNamePrefix, commonConfig.ServiceDomain);
        }

        public static string GetMongoDataHost()
        {
            return string.Format("{0}-mdb.{1}", commonConfig.ServiceNamePrefix, commonConfig.ServiceDomain);
        }

        public static int GetSshPort(int routerId)
        {
            return commonConfig.PortMapping ? 22000 + routerId : 22;
        }

        public static int GetMongoDataPort(int routerId)
        {
            return commonConfig.PortMapping ? 27000 + routerId : 27017;
        }

        public static int GetMongoSshPort(int shardId, int replicaId)
        {
            return commonConfig.PortMapping ? 22000 + shardId * 50 + replicaId : 22;
        }

        public static int GetMongoDataPort(int shardId, int replicaId)
        {
            return commonConfig.PortMapping ? 27000 + shardId * 50 + replicaId : 27017;
        }

        public static int GetMongoConfPort(int shardId, int replicaId)
        {
            return commonConfig.PortMapping ? 28000 + shardId * 50 + replicaId : 27018;
        }

        public static string GetMongoShardName(int shardId)
        {
            return "SHARD-" + ((char)('A' + shardId)).ToString();
        }

        public static string GetElasticRouterHost()
        {
            return string.Format("{0}-esr.{1}", commonConfig.ServiceNamePrefix, commonConfig.ServiceDomain);
        }

        public static string GetElasticDataHost()
        {
            return string.Format("{0}-esd.{1}", commonConfig.ServiceNamePrefix, commonConfig.ServiceDomain);
        }

        public static int GetElasticDataPort(int nodeId)
        {
            return commonConfig.PortMapping ? 9200 + nodeId : 9200;
        }

        public static string GetCassandraDataHost()
        {
            return string.Format("{0}-cdb.{1}", commonConfig.ServiceNamePrefix, commonConfig.ServiceDomain);
        }

        public static int GetCassandraDataPort(int nodeId)
        {
            return commonConfig.PortMapping ? 7000 + nodeId : 7000;
        }

        public static bool IsOdd(int v)
        {
            return !IsEven(v);
        }

        public static bool IsEven(int v)
        {
            return v % 2 == 0;
        }

        public static string GetConfigDbList()
        {
            var configDbList = string.Empty;
            var dataHost     = GetMongoDataHost();

            // Determine where we're going to host the shard configuration databases.  Note that
            // Mongo requires either 1 or 3 databases.  Note that all DATA servers will have a 
            // MongoDB instance running on a 28### port so all we need to do is set [configDbList]
            // to a comma separated list of <host>:<port> for the chosen instances.

            if (mongoConfig.ShardCount >= 3)
            {
                // Three config databases will be located on the first replica server on the
                // first three shards.

                for (int shardId = 0; shardId < 3; shardId++)
                {
                    if (configDbList.Length > 0)
                    {
                        configDbList += ",";
                    }

                    configDbList += string.Format("{0}:{1}", dataHost, GetMongoConfPort(shardId, 0));
                }
            }
            else if (mongoConfig.ReplicaCount >= 3)
            {
                // There aren't enough shards to distribute the config servers to but there
                // are enough replicas.  So, we'll locate the config servers to the first
                // three replicas of shard 0.

                for (int replicaId = 0; replicaId < 3; replicaId++)
                {
                    if (configDbList.Length > 0)
                    {
                        configDbList += ",";
                    }

                    configDbList += string.Format("{0}:{1}", dataHost, GetMongoConfPort(0, replicaId));
                }
            }
            else if (mongoConfig.ShardCount * mongoConfig.ReplicaCount >= 3)
            {
                // Locate config servers across shards and replicas.

                int configDbCount = 0;

                for (int shardId = 0; shardId < mongoConfig.ShardCount; shardId++ )
                {
                    for (int replicaId = 0; replicaId < mongoConfig.ReplicaCount; replicaId++)
                    {
                        if (configDbList.Length > 0)
                        {
                            configDbList += ",";
                        }

                        configDbList += string.Format("{0}:{1}", dataHost, GetMongoConfPort(shardId, replicaId));

                        if (++configDbCount >= 3)
                        {
                            return configDbList;
                        }
                    }
                }
            }
            else
            {
                // There aren't enough DATA servers to have three config databases so
                // locate a single database on the shard 0, replica 0.  This configuration
                // should not be used for production.

                configDbList = string.Format("{0}:{1}", dataHost, GetMongoConfPort(0, 0));
            }

            return configDbList;
        }
    }
}
