cmake_minimum_required(VERSION 3.16)

if(VITA_BUILD)
  if(NOT DEFINED ENV{VITASDK})
    message(FATAL_ERROR "Set VITASDK to your Vita SDK directory")
  endif()
  set(CMAKE_TOOLCHAIN_FILE "$ENV{VITASDK}/share/vita.toolchain.cmake" CACHE PATH "Vita toolchain")
endif()

project(ladxhd C CXX)

file(STRINGS "${CMAKE_SOURCE_DIR}/RELEASE_VERSION" DEFAULT_RELEASE_VERSION LIMIT_COUNT 1)
if(NOT DEFINED LADX_RELEASE_VERSION)
  set(LADX_RELEASE_VERSION "${DEFAULT_RELEASE_VERSION}")
endif()
file(READ "${CMAKE_SOURCE_DIR}/../game/Directory.Build.props" GAME_PROPS)
string(REGEX MATCH "<GameVersion>([^<]+)</GameVersion>" GAME_VERSION_MATCH "${GAME_PROPS}")
set(LADX_GAME_VERSION "${CMAKE_MATCH_1}")
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS
  "${CMAKE_SOURCE_DIR}/RELEASE_VERSION"
  "${CMAKE_SOURCE_DIR}/../game/Directory.Build.props")

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS ON)
if(NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE RelWithDebInfo)
endif()

set(GEN_DIR "${CMAKE_SOURCE_DIR}/build/gen")
file(GLOB GEN_SOURCES "${GEN_DIR}/*.cpp")
if(NOT GEN_SOURCES)
  message(FATAL_ERROR "Run tools/check.sh (or the transpiler gen command) first to create build/gen")
endif()

file(GLOB RUNTIME_SOURCES "${CMAKE_SOURCE_DIR}/runtime/src/*.cpp")
set(FRAMEWORK_SOURCES
  framework/src/math.cpp
  framework/src/gfx_common.cpp
  framework/src/effects.cpp
  framework/src/content.cpp
  framework/src/audio.cpp
  framework/src/game.cpp
  framework/src/overlay.cpp
  framework/src/stb_vorbis_impl.c)

# Boehm GC, single-threaded (all managed code runs on the main thread).
add_library(bdwgc STATIC third_party/bdwgc/extra/gc.c)
target_include_directories(bdwgc PUBLIC third_party/bdwgc/include)
target_compile_definitions(bdwgc PRIVATE GC_DISABLE_INCREMENTAL NO_EXECUTE_PERMISSION ALL_INTERIOR_POINTERS LARGE_CONFIG)
target_compile_options(bdwgc PRIVATE -O2 -w)
if(VITA_BUILD)
  # Vita is detected as ARM32 NOSYS: roots = __data_start..end, stack bottom from __stack_base__.
  target_compile_definitions(bdwgc PRIVATE HAVE_BUILTIN_UNWIND_INIT NO_GETCONTEXT GC_DONT_REGISTER_MAIN_STATIC_DATA)
endif()

set(COMMON_INCLUDES
  ${CMAKE_SOURCE_DIR}/runtime/include
  ${CMAKE_SOURCE_DIR}/framework/include
  ${CMAKE_SOURCE_DIR}/third_party
  ${GEN_DIR})

add_library(gamecode STATIC ${GEN_SOURCES})
target_include_directories(gamecode PUBLIC ${COMMON_INCLUDES})
target_compile_options(gamecode PRIVATE -fwrapv -fno-strict-aliasing -w)
target_link_libraries(gamecode PUBLIC bdwgc)

add_library(runtime STATIC ${RUNTIME_SOURCES})
target_include_directories(runtime PUBLIC ${COMMON_INCLUDES})
target_compile_options(runtime PRIVATE -fwrapv -fno-strict-aliasing -Wall -Wno-unused-function)
target_link_libraries(runtime PUBLIC bdwgc z)
# Background save writer (runtime/src/io.cpp).
if(VITA_BUILD)
  target_link_libraries(runtime PUBLIC pthread)
else()
  find_package(Threads REQUIRED)
  target_link_libraries(runtime PUBLIC Threads::Threads)
endif()

if(VITA_BUILD)
  include("$ENV{VITASDK}/share/vita.cmake" REQUIRED)
  add_library(framework STATIC ${FRAMEWORK_SOURCES} framework/src/platform_vita.cpp framework/src/gfx_gxm.cpp)
  target_include_directories(framework PUBLIC ${COMMON_INCLUDES})
  target_compile_definitions(framework PUBLIC FW_VITA)
  target_compile_options(framework PRIVATE -fwrapv -fno-strict-aliasing $<$<COMPILE_LANGUAGE:C>:-w>)
  # Raw GXM; the shader compiler stub is weak so the game still starts from bundled/cached GXP without libshacccg.
  target_link_libraries(framework PUBLIC bdwgc
    SceGxm_stub SceDisplay_stub SceShaccCg_stub_weak SceAppMgr_stub SceAppUtil_stub SceCommonDialog_stub
    SceSysmodule_stub SceIofilemgr_stub SceCtrl_stub SceTouch_stub SceAudio_stub ScePower_stub SceLibKernel_stub
    pthread m)

  add_executable(ladxhd port/src/main.cpp)
  target_compile_options(ladxhd PRIVATE -fwrapv -w)
  target_link_libraries(ladxhd PRIVATE gamecode framework runtime bdwgc)
  vita_create_self(eboot.bin ladxhd UNSAFE)
else()
  find_package(PkgConfig REQUIRED)
  pkg_check_modules(SDL2 REQUIRED IMPORTED_TARGET sdl2)
  add_library(framework STATIC ${FRAMEWORK_SOURCES} framework/src/platform_sdl.cpp framework/src/gfx_gl.cpp)
  target_include_directories(framework PUBLIC ${COMMON_INCLUDES})
  target_compile_options(framework PRIVATE -fwrapv -fno-strict-aliasing $<$<COMPILE_LANGUAGE:CXX>:-Wall -Wno-unused-function> $<$<COMPILE_LANGUAGE:C>:-w>)
  target_link_libraries(framework PUBLIC PkgConfig::SDL2 bdwgc)
  if(APPLE)
    target_link_libraries(framework PUBLIC "-framework OpenGL")
  else()
    target_link_libraries(framework PUBLIC GL)
  endif()

  add_executable(ladxhd port/src/main.cpp)
  target_compile_options(ladxhd PRIVATE -fwrapv -w)
  target_link_libraries(ladxhd PRIVATE gamecode framework runtime bdwgc)
endif()

target_compile_definitions(ladxhd PRIVATE
  LADX_RELEASE_VERSION="${LADX_RELEASE_VERSION}"
  LADX_GAME_VERSION="${LADX_GAME_VERSION}")

option(LADX_BUILD_TESTS "Build desktop runtime regression tests" OFF)
if(LADX_BUILD_TESTS AND NOT VITA_BUILD)
  enable_testing()
  add_executable(runtime_collections_test runtime/tests/collections.cpp)
  target_link_libraries(runtime_collections_test PRIVATE runtime)
  target_compile_options(runtime_collections_test PRIVATE -O2 -UNDEBUG)
  add_test(NAME runtime_collections COMMAND runtime_collections_test)
  add_executable(overlay_test runtime/tests/overlay.cpp framework/src/overlay.cpp)
  target_include_directories(overlay_test PRIVATE ${COMMON_INCLUDES})
  target_compile_options(overlay_test PRIVATE -UNDEBUG)
  add_test(NAME overlay COMMAND overlay_test)
endif()
