#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <shellapi.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Media.Control.h>
#include <winrt/Windows.Storage.Streams.h>
#include <filesystem>
#include <thread>
#include <mutex>
#include <condition_variable>
#include <deque>
#include <vector>
#include <map>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cwctype>
#include "DeskModel.h"
#include "Weather.h"
#include "AlbumCover.h"
using namespace winrt;
using namespace Windows::Media::Control;
using namespace std::chrono_literals;
namespace fs=std::filesystem;
namespace {
struct Project {fs::path path;fs::file_time_type modified;};
struct Media {std::wstring title=L"Rien en lecture",artist=L"",source=L"Spotify · YouTube",id,error;bool exists=false,playing=false,play=false,next=false,previous=false,seek=false;double position=0,duration=0;int sources=0;CoverBytes cover;};
struct Command {std::wstring action,id;};
std::mutex gate;
std::condition_variable wake;
std::atomic<bool> stopping=false;
std::thread mediaThread,projectThread,weatherThread;
std::atomic<HINTERNET> weatherRequest=nullptr;
std::atomic<bool> refreshWeather=false;
std::wstring weatherCity,weatherLat,weatherLon;
DeskWeather weather;
std::deque<Command> commands;
Media media;
std::vector<Project> projects;
fs::path projectRoot,selected;
std::wstring projectStatus=L"Analyse…";

std::wstring Ini(const wchar_t* key,const std::wstring& file,const wchar_t* fallback=L""){
    wchar_t result[4096];GetPrivateProfileStringW(L"Desk",key,fallback,result,4096,file.c_str());
    wchar_t expanded[8192];ExpandEnvironmentStringsW(result,expanded,8192);return expanded;
}
std::wstring Friendly(std::wstring id){auto name=id;std::transform(name.begin(),name.end(),name.begin(),towlower);if(name.find(L"spotify")!=name.npos)return L"Spotify";if(name.find(L"brave")!=name.npos)return L"Brave";if(name.find(L"chrome")!=name.npos)return L"Chrome";if(name.find(L"edge")!=name.npos)return L"Edge";return L"Média Windows";}
template<typename T> auto Await(T op){if(op.wait_for(1500ms)!=Windows::Foundation::AsyncStatus::Completed){op.Cancel();throw hresult_error(E_ABORT);}return op.get();}
std::wstring Time(double seconds){int s=(int)std::max(0.0,seconds);wchar_t b[40];swprintf_s(b,L"%02d:%02d",s/60,s%60);return b;}
void RunMedia(){
    init_apartment(apartment_type::multi_threaded);
    GlobalSystemMediaTransportControlsSessionManager manager{nullptr};
    std::wstring pinned;
    std::wstring coverKey;CoverBytes cover;uint64_t coverRetry=0;
    while(!stopping){
        try{
            if(!manager)manager=Await(GlobalSystemMediaTransportControlsSessionManager::RequestAsync());
            auto all=manager.GetSessions();auto current=manager.GetCurrentSession();
            if(!pinned.empty()){bool found=false;for(auto s:all)if(s.SourceAppUserModelId()==pinned){current=s;found=true;break;}if(!found)pinned.clear();}
            std::deque<Command> pending;{std::lock_guard lock(gate);pending.swap(commands);}
            for(auto& command:pending){
                if(command.action==L"Source"){
                    if(all.Size()){uint32_t next=0;for(uint32_t i=0;i<all.Size();i++)if(current&&all.GetAt(i).SourceAppUserModelId()==current.SourceAppUserModelId())next=(i+1)%all.Size();current=all.GetAt(next);pinned=current.SourceAppUserModelId();}continue;
                }
                GlobalSystemMediaTransportControlsSession target{nullptr};for(auto s:all)if(s.SourceAppUserModelId()==command.id){target=s;break;}
                if(!target)continue;
                bool ok=false;auto capabilities=target.GetPlaybackInfo().Controls();
                if(command.action==L"Play"&&capabilities.IsPlayPauseToggleEnabled())ok=Await(target.TryTogglePlayPauseAsync());
                else if(command.action==L"Next"&&capabilities.IsNextEnabled())ok=Await(target.TrySkipNextAsync());
                else if(command.action==L"Previous"&&capabilities.IsPreviousEnabled())ok=Await(target.TrySkipPreviousAsync());
                else if(command.action.rfind(L"Seek:",0)==0&&capabilities.IsPlaybackPositionEnabled()){
                    double ratio=std::clamp(std::stod(command.action.substr(5)),0.0,1.0);auto line=target.GetTimelineProperties();
                    ok=Await(target.TryChangePlaybackPositionAsync(line.StartTime().count()+(int64_t)((line.EndTime()-line.StartTime()).count()*ratio)));
                }
                {std::lock_guard lock(gate);media.error=ok?L"":L"Commande non disponible";}
            }
            Media next;next.sources=(int)all.Size();
            if(current){
                auto properties=Await(current.TryGetMediaPropertiesAsync());auto playback=current.GetPlaybackInfo();auto controls=playback.Controls();auto line=current.GetTimelineProperties();
                next.exists=true;next.title=properties.Title().empty()?L"Média sans titre":std::wstring(properties.Title());next.artist=properties.Artist();next.id=current.SourceAppUserModelId();next.source=Friendly(next.id);
                next.playing=playback.PlaybackStatus()==GlobalSystemMediaTransportControlsSessionPlaybackStatus::Playing;
                next.play=controls.IsPlayPauseToggleEnabled();next.next=controls.IsNextEnabled();next.previous=controls.IsPreviousEnabled();next.seek=controls.IsPlaybackPositionEnabled();
                next.duration=std::max(0.0,(line.EndTime()-line.StartTime()).count()/10000000.0);
                double position=(line.Position()-line.StartTime()).count()/10000000.0;
                if(next.playing){auto rate=playback.PlaybackRate();position+=std::max(0.0,(winrt::clock::now()-line.LastUpdatedTime()).count()/10000000.0)*(rate?rate.Value():1.0);}
                next.position=std::clamp(position,0.0,next.duration);next.seek=next.seek&&next.duration>0;
                auto key=next.id+L"\n"+next.title+L"\n"+next.artist;
                if(key!=coverKey){coverKey=key;cover.reset();coverRetry=0;}
                if(!cover&&GetTickCount64()>=coverRetry){
                    coverRetry=GetTickCount64()+5000;
                    try{if(auto thumbnail=properties.Thumbnail()){
                        auto stream=Await(thumbnail.OpenReadAsync());auto size=stream.Size();
                        if(size>0&&size<=4*1024*1024){Windows::Storage::Streams::DataReader reader(stream.GetInputStreamAt(0));auto loaded=Await(reader.LoadAsync((uint32_t)size));
                            if(loaded==size){auto bytes=std::make_shared<std::vector<uint8_t>>(loaded);reader.ReadBytes(*bytes);cover=std::move(bytes);}}
                    }}catch(...){/* Keep transport controls available when artwork is absent. */}
                }
                next.cover=cover;
            }else{cover.reset();coverKey.clear();}
            {std::lock_guard lock(gate);next.error=media.error;media=std::move(next);}
        }catch(...){std::lock_guard lock(gate);media=Media{};media.source=L"Médias indisponibles";manager=nullptr;}
        std::unique_lock lock(gate);wake.wait_for(lock,1s,[]{return stopping||!commands.empty();});
    }
    uninit_apartment();
}
void ScanProjects(){
    SetThreadPriority(GetCurrentThread(),THREAD_MODE_BACKGROUND_BEGIN);
    while(!stopping){
        std::vector<Project> next;bool incomplete=false;size_t entries=0;std::error_code ec;
        auto start=std::chrono::steady_clock::now();
        for(fs::directory_iterator top(projectRoot,fs::directory_options::skip_permission_denied,ec),end;top!=end&&!stopping;top.increment(ec)){
            if(ec){incomplete=true;ec.clear();continue;}
            auto item=*top;if(!item.is_directory(ec)||item.is_symlink(ec)||IgnoreProjectDirectory(item.path().filename()))continue;
            auto latest=fs::file_time_type::min();
            for(fs::recursive_directory_iterator it(item.path(),fs::directory_options::skip_permission_denied,ec),last;it!=last&&!stopping;it.increment(ec)){
                if(ec){incomplete=true;ec.clear();continue;}
                const auto& child=*it;auto name=child.path().filename().wstring();
                auto attributes=GetFileAttributesW(child.path().c_str());
                if((attributes!=INVALID_FILE_ATTRIBUTES&&(attributes&FILE_ATTRIBUTE_REPARSE_POINT))||IgnoreProjectDirectory(name)){it.disable_recursion_pending();continue;}
                if(child.is_regular_file(ec)&&name!=L"auth.json"&&name!=L"wallpaper-token.txt"&&child.path().extension()!=L".log"){
                    auto stamp=child.last_write_time(ec);if(!ec)latest=std::max(latest,stamp);
                }
                if(++entries>1000000){incomplete=true;break;}
            }
            if(latest!=fs::file_time_type::min())next.push_back({item.path(),latest});
            if(entries>1000000)break;
        }
        if(ec)incomplete=true;
        std::sort(next.begin(),next.end(),[](auto&a,auto&b){return a.modified==b.modified?a.path<b.path:a.modified>b.modified;});
        if(next.size()>6)next.resize(6);
        {std::lock_guard lock(gate);projects=std::move(next);if(selected.empty()&&!projects.empty())selected=projects[0].path;projectStatus=incomplete?L"Analyse partielle":projects.empty()?L"Aucun projet":L"6 derniers projets";}
        for(int i=0;i<600&&!stopping;i++)std::this_thread::sleep_for(100ms);
    }
}
void RunWeather(){
    init_apartment(apartment_type::multi_threaded);
    while(!stopping){
        if(!weatherCity.empty()){
            try{auto next=FetchWeather(weatherLat,weatherLon,weatherRequest,stopping);std::lock_guard lock(gate);weather=std::move(next);}
            catch(...){std::lock_guard lock(gate);weather.stale=true;}
        }
        refreshWeather=false;
        for(int i=0;i<9000&&!stopping&&!refreshWeather;i++)std::this_thread::sleep_for(100ms);
    }
    uninit_apartment();
}
void Start(const std::wstring& config){
    {std::lock_guard lock(gate);projects.clear();weather=DeskWeather{};projectStatus=L"Analyse…";}
    projectRoot=Ini(L"ProjectRoot",config);
    if(selected.parent_path()!=projectRoot)selected.clear();
    weatherCity=Ini(L"WeatherCity",config);weatherLat=Ini(L"WeatherLatitude",config);weatherLon=Ini(L"WeatherLongitude",config);
    stopping=false;mediaThread=std::thread(RunMedia);projectThread=std::thread(ScanProjects);weatherThread=std::thread(RunWeather);
}
bool Open(const fs::path& path,const std::wstring& args=L""){if(!fs::exists(path))return false;return (INT_PTR)ShellExecuteW(nullptr,L"open",path.c_str(),args.empty()?nullptr:args.c_str(),nullptr,SW_SHOWNORMAL)>32;}
std::wstring Read(std::wstring key){
    if(key==L"mouseX"){POINT point;GetCursorPos(&point);return std::to_wstring(point.x);}
    if(key.rfind(L"clock",0)==0){SYSTEMTIME time;GetLocalTime(&time);if(key==L"clockHours")return ClockDigits(time.wHour);if(key==L"clockMinutes")return ClockDigits(time.wMinute);if(key==L"clockSeconds")return ClockDigits(time.wSecond);if(key==L"clockDate"){wchar_t date[128];GetDateFormatEx(LOCALE_NAME_USER_DEFAULT,DATE_LONGDATE,&time,nullptr,date,128,nullptr);return date;}}
    std::lock_guard lock(gate);
    if(key.rfind(L"weather",0)==0){auto number=[](double n){return std::isfinite(n)?std::to_wstring((int)std::round(n)):L"—";};
        if(key==L"weatherCity")return weatherCity.empty()?L"Ville à définir":weatherCity;
        if(key==L"weatherTemp")return number(weather.temperature)+L"°";
        if(key==L"weatherCondition")return weather.loaded?WeatherLabel(weather.code)+(weather.stale?L" · ancienne mesure":L""):L"Météo indisponible";
        if(key==L"weatherRange")return L"↑ "+number(weather.maximum)+L"°   ↓ "+number(weather.minimum)+L"°";
        if(key==L"weatherWind")return L"Vent · "+number(weather.wind)+L" km/h";
        if(key==L"weatherTime")return weather.loaded?L"Open-Meteo · "+weather.time:L"Open-Meteo";
        if(key==L"weatherIcon")return WeatherIcon(weather.code,weather.day);
    }
    if(key==L"title")return media.title;if(key==L"artist")return media.artist;if(key==L"source")return media.source;
    if(key==L"mediaExists")return media.exists?L"1":L"0";if(key==L"playing")return media.playing?L"1":L"0";
    if(key==L"canPlay")return media.play?L"1":L"0";if(key==L"canNext")return media.next?L"1":L"0";if(key==L"canPrevious")return media.previous?L"1":L"0";if(key==L"canSeek")return media.seek?L"1":L"0";
    if(key==L"mediaProgress")return std::to_wstring(media.duration>0?media.position/media.duration:0);
    if(key==L"mediaTime")return media.exists?Time(media.position)+L" / "+Time(media.duration):L"";
    if(key==L"mediaError")return media.error;if(key==L"sourceCount")return std::to_wstring(media.sources);
    if(key==L"projectStatus")return projectStatus;if(key==L"selectedProject")return selected.filename();
    if(key==L"selectedPath")return selected.wstring();
    if(key.rfind(L"project:",0)==0){int i=-1;wchar_t field[20];if(swscanf_s(key.c_str(),L"project:%d:%19s",&i,field,20)==2&&i>=0&&i<(int)projects.size()){
        if(wcscmp(field,L"name")==0)return projects[i].path.filename();
        if(wcscmp(field,L"path")==0)return projects[i].path.wstring();
        if(wcscmp(field,L"age")==0){auto minutes=std::chrono::duration_cast<std::chrono::minutes>(fs::file_time_type::clock::now()-projects[i].modified).count();return minutes<1?L"à l'instant":minutes<60?std::to_wstring(minutes)+L" min":minutes<1440?std::to_wstring(minutes/60)+L" h":std::to_wstring(minutes/1440)+L" j";}
    }return L"";}
    return L"";
}
void Execute(std::wstring command){
    if(command.rfind(L"Project:",0)==0){int i=std::stoi(command.substr(8));fs::path path;{std::lock_guard lock(gate);if(i<0||i>=(int)projects.size())return;selected=path=projects[i].path;}Open(path);return;}
    if(command.rfind(L"Select:",0)==0){int i=std::stoi(command.substr(7));std::lock_guard lock(gate);if(i>=0&&i<(int)projects.size())selected=projects[i].path;return;}
    if(command==L"OpenSelected"){fs::path path;{std::lock_guard lock(gate);path=selected;}if(!path.empty())Open(path);return;}
    std::lock_guard lock(gate);
    if(command==L"WeatherRefresh"){refreshWeather=true;return;}
    if(command==L"Play"||command==L"Next"||command==L"Previous"||command==L"Source"||command.rfind(L"Seek:",0)==0){commands.push_back({command,media.id});wake.notify_all();}
}
void Stop(){stopping=true;wake.notify_all();if(auto request=weatherRequest.exchange(nullptr))WinHttpCloseHandle(request);if(mediaThread.joinable())mediaThread.join();if(projectThread.joinable())projectThread.join();if(weatherThread.joinable())weatherThread.join();}
}
extern "C" __declspec(dllexport) void DeskStart(const wchar_t* config){Start(config);}
extern "C" __declspec(dllexport) void DeskStop(){Stop();}
extern "C" __declspec(dllexport) int DeskRead(const wchar_t* key,wchar_t* buffer,int capacity){
    try{auto value=Read(key);if(capacity<1)return 0;auto length=std::min<size_t>(value.size(),capacity-1);std::copy_n(value.c_str(),length,buffer);buffer[length]=0;return (int)length;}catch(...){if(capacity>0)buffer[0]=0;return 0;}
}
extern "C" __declspec(dllexport) void DeskCommand(const wchar_t* command){try{Execute(command);}catch(...){}}
extern "C" __declspec(dllexport) int DeskCover(unsigned char* buffer,int capacity){
    std::lock_guard lock(gate);if(!media.cover)return 0;int size=(int)media.cover->size();if(buffer&&capacity>=size)std::copy(media.cover->begin(),media.cover->end(),buffer);return size;
}
#ifdef DESK_PROBE
#include <iostream>
int wmain(int argc,wchar_t**argv){if(argc<2)return 2;
if(std::wstring(argv[1])==L"--contracts"){
    init_apartment(apartment_type::multi_threaded);
    auto valid=ParseWeather(LR"({"current":{"temperature_2m":0,"weather_code":999}})");
    if(!valid.loaded||valid.temperature!=0||std::isfinite(valid.wind)||WeatherIcon(valid.code,true)!=L"weather-unknown")return 6;
    bool rejected=false;try{ParseWeather(LR"({"current":{}})");}catch(...){rejected=true;}
    if(!rejected)return 7;
    {
        Gdiplus::GdiplusStartupInput input;ULONG_PTR token;Gdiplus::GdiplusStartup(&token,&input,nullptr);
        CoverBytes bytes;
        {Gdiplus::Bitmap sample(32,32);Gdiplus::Graphics graphics(&sample);graphics.Clear(Gdiplus::Color(255,90,160,220));
            winrt::com_ptr<IStream> stream;CreateStreamOnHGlobal(nullptr,TRUE,stream.put());
            const CLSID png={0x557cf406,0x1a04,0x11d3,{0x9a,0x73,0,0,0xf8,0x1e,0xf3,0x2e}};
            if(sample.Save(stream.get(),&png,nullptr)!=Gdiplus::Ok)return 8;
            STATSTG stat{};stream->Stat(&stat,STATFLAG_NONAME);auto data=std::make_shared<std::vector<uint8_t>>((size_t)stat.cbSize.QuadPart);
            LARGE_INTEGER zero{};stream->Seek(zero,STREAM_SEEK_SET,nullptr);ULONG read=0;stream->Read(data->data(),(ULONG)data->size(),&read);bytes=data;}
        auto owner=CreateWindowExW(WS_EX_TOOLWINDOW,L"STATIC",L"Album memory test",WS_POPUP,-3000,-3000,120,120,nullptr,nullptr,GetModuleHandleW(nullptr),nullptr);
        bool passed=false;{AlbumCover cover;passed=cover.Update(owner,10,10,64,bytes)&&!cover.Update(owner,10,10,64,{})&&!cover.Update(owner,10,10,64,std::make_shared<const std::vector<uint8_t>>(3,0));}
        DestroyWindow(owner);Gdiplus::GdiplusShutdown(token);if(!passed)return 9;
    }
    uninit_apartment();
    std::cout<<"Weather/cover contracts passed: unknown fields, real zero, missing temperature, in-memory artwork.\n";return 0;
}
Start(argv[1]);for(int i=0;i<60&&Read(L"projectStatus")==L"Analyse…";i++)std::this_thread::sleep_for(500ms);std::this_thread::sleep_for(2s);for(auto key:{L"sourceCount",L"mediaExists",L"source",L"canPlay",L"projectStatus",L"project:0:name",L"project:1:name",L"project:2:name",L"project:3:name",L"project:4:name",L"project:5:name"})std::cout<<to_string(key)<<"="<<to_string(Read(key))<<"\n";Stop();return 0;}
#endif
