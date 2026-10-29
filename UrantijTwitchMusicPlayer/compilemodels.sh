# удали компайлед модели и закоментить юзание модели

dotnet ef dbcontext optimize --precompile-queries --nativeaot --output-dir CompiledModels --namespace UrantijTwitchMusicPlayer.CompiledModels

# :)
sed -i 's/urantijTwitchMusicPlayer\.Shared\.Data\.Models\.TrackCachedDataPrimaryKeyProperties/urantijTwitchMusicPlayerSharedDataModelsTrackCachedDataPrimaryKeyProperties/g' ./CompiledModels/Database.EFInterceptors.MyContext.cs
sed -i 's/urantijTwitchMusicPlayer\.Shared\.Data\.Models\.RequestModelPrimaryKeyProperties/urantijTwitchMusicPlayerSharedDataModelsRequestModelPrimaryKeyProperties/g' ./CompiledModels/Database.EFInterceptors.MyContext.cs

# верни юзание модели