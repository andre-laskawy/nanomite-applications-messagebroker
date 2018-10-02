@rem Copyright 2016 gRPC authors.
@rem
@rem Licensed under the Apache License, Version 2.0 (the "License");
@rem you may not use this file except in compliance with the License.
@rem You may obtain a copy of the License at
@rem
@rem     http://www.apache.org/licenses/LICENSE-2.0
@rem
@rem Unless required by applicable law or agreed to in writing, software
@rem distributed under the License is distributed on an "AS IS" BASIS,
@rem WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
@rem See the License for the specific language governing permissions and
@rem limitations under the License.

@rem Generate the C# code for .proto files

setlocal

set PROTO_EXE_PATH=%cd%\compiler

set PROTO_PATH_MODELS=%cd%\GRPC.MODELS
set CHARP_PATH_MODELS= %cd%\..\Models\Protos

IF EXIST %CHARP_PATH_MODELS% (
    rmdir %CHARP_PATH_MODELS% /s /q
)

mkdir %CHARP_PATH_MODELS%

for /f %%f in ('dir /b %cd%\GRPC.MODELS\*.proto') DO (
	%PROTO_EXE_PATH%\protoc.exe -I %PROTO_PATH_MODELS% --csharp_out %CHARP_PATH_MODELS% --grpc_out %CHARP_PATH_MODELS% %PROTO_PATH_MODELS%\%%f --plugin=protoc-gen-grpc=%PROTO_EXE_PATH%\grpc_csharp_plugin.exe
)