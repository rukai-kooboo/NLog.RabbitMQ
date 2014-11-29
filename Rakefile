require 'bundler/setup'

require 'albacore'
require 'albacore/tasks/release'
require 'albacore/tasks/versionizer'
require 'albacore/ext/teamcity'

Configuration = 'Release'

Albacore::Tasks::Versionizer.new :versioning

desc 'create assembly infos'
asmver_files :assembly_info do |a|
  a.files = FileList['**/*proj'] # optional, will find all projects recursively by default

  a.attributes assembly_description: 'A RabbitMQ appender for NLog. See https://github.com/haf/NLog.RabbitMQ for documentation!',
               assembly_configuration: Configuration,
               assembly_company: 'Logibit AB',
               assembly_copyright: "(c) 2014 by Henrik Feldt",
               assembly_version: ENV['LONG_VERSION'],
               assembly_file_version: ENV['LONG_VERSION'],
               assembly_informational_version: ENV['BUILD_VERSION']
end

desc 'Perform fast build (warn: doesn\'t d/l deps)'
build :quick_compile do |b|
  b.logging = 'detailed'
  b.sln     = 'src/NLog.Targets.RabbitMQ.sln'
end

task :paket_bootstrap do
system 'tools/paket.bootstrapper.exe', clr_command: true unless File.exists? 'tools/paket.exe'
end

desc 'restore all nugets as per the packages.config files'
task :restore => :paket_bootstrap do
  system 'tools/paket.exe', 'restore', clr_command: true
end

desc 'Perform full build'
build :compile => [:versioning, :restore, :assembly_info] do |b|
  b.sln = 'src/NLog.Targets.RabbitMQ.sln'
end

directory 'build/pkg'

desc 'package nugets - finds all projects and package them'
nugets_pack :create_nugets => ['build/pkg', :versioning, :compile] do |p|
  p.files   = FileList['src/**/*.{csproj,fsproj,nuspec}'].
    exclude(/Tests/)
  p.out     = 'build/pkg'
  p.exe     = 'packages/NuGet.CommandLine/tools/NuGet.exe'
  p.with_metadata do |m|
    m.id          = 'NLog.RabbitMQ'
    m.title       = 'NLog RabbitMQ Target'
    m.description = 'A RabbitMQ appender for NLog. See https://github.com/haf/NLog.RabbitMQ for documentation!'
    m.authors     = 'Henrik Feldt'
    m.project_url = 'https://github.com/haf/NLog.RabbitMQ'
    m.tags        = 'rabbitmq messaging logs logging nlog target sink'
    m.version     = ENV['NUGET_VERSION']
  end
end

namespace :tests do
  #task :unit do
  #  system "src/MyProj.Tests/bin/#{Configuration}"/MyProj.Tests.exe"
  #end
end

# task :tests => :'tests:unit'

task :default => :create_nugets #, :tests ]

task :ensure_nuget_key do
  raise 'missing env NUGET_KEY value' unless ENV['NUGET_KEY']
end

Albacore::Tasks::Release.new :release,
                             pkg_dir: 'build/pkg',
                             depend_on: [:create_nugets, :ensure_nuget_key],
                             nuget_exe: 'packages/NuGet.CommandLine/tools/NuGet.exe',
                             api_key: ENV['NUGET_KEY']

namespace :todo do
  task :nr_output => [:msbuild] do
    raise 'todo'
    target = File.join(FOLDERS[:binaries], PROJECTS[:nr][:id])
    copy_files FOLDERS[:nr][:out], "*.{xml,dll}", target
    CLEAN.include(target)
  end

  task :l_output => [:msbuild] do
    raise 'todo'
    FileUtils.mkdir_p "build/#{FRAMEWORK}/#{CONFIGURATION}/listener"
    copy_files "src/nlog-rmq-listener/bin/#{CONFIGURATION}", "*.{config,exe,dll}", "build/#{FRAMEWORK}/#{CONFIGURATION}/listener"
    copy_files "src/nlog-rmq-listener/bin/#{CONFIGURATION}", "*.{config,exe,dll}", "build/nuspec/NLog.RabbitMQ/tools"
  end

  task :output => [:nr_output, :l_output]

  task :ilmerge => :output do |cfg|
    raise 'todo'
    folder = File.join(FOLDERS[:binaries], PROJECTS[:nr][:id])
    Dir.chdir folder do |d|
      FileUtils.mkdir 'tmp'
      Dir.glob("./*.*").each{ |f| FileUtils.mv f, 'tmp' }
      system '../../../../tools/ILMerge.exe /internalize /allowDup /xmldocs tmp\NLog.Targets.RabbitMQ.dll tmp\Newtonsoft.Json.dll /out:NLog.Targets.RabbitMQ.dll /v4'
      FileUtils.rm_rf 'tmp'
    end
  end
end
