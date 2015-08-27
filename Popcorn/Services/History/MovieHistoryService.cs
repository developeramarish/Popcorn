﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Popcorn.Entity;
using Popcorn.Entity.Cast;
using Popcorn.Entity.Movie;
using Popcorn.Models.Movie;
using Popcorn.Models.Torrent.Deserialized;
using MovieFull = Popcorn.Models.Movie.MovieFull;
using MovieShort = Popcorn.Models.Movie.MovieShort;

namespace Popcorn.Services.History
{
    /// <summary>
    /// Services used to interacts with movie history
    /// </summary>
    public class MovieHistoryService
    {
        #region Logger

        /// <summary>
        /// Logger of the class
        /// </summary>
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        #endregion

        #region Methods

        #region Method -> ComputeMovieHistoryAsync

        /// <summary>
        /// Retrieve from database and set the IsFavorite and HasBeenSeen properties of each movie in params, 
        /// </summary>
        /// <param name="movies">All movies to compute</param>
        /// <param name="ct">Used to cancel task</param>
        public async Task ComputeMovieHistoryAsync(IEnumerable<MovieShort> movies, CancellationTokenSource ct)
        {
            var watch = Stopwatch.StartNew();

            try
            {
                await Task.Run(async () =>
                {
                    using (var context = new ApplicationDbContext())
                    {
                        await context.MovieHistory.LoadAsync(ct.Token);
                        var history = await context.MovieHistory.FirstOrDefaultAsync(ct.Token);
                        if (history == null)
                        {
                            await CreateMovieHistoryAsync(ct);
                            history = await context.MovieHistory.FirstOrDefaultAsync(ct.Token);
                        }

                        foreach (var movie in movies.ToList())
                        {
                            var entityMovie = history.MoviesShort.FirstOrDefault(p => p.MovieId == movie.Id);
                            if (entityMovie == null) continue;
                            movie.IsFavorite = entityMovie.IsFavorite;
                            movie.HasBeenSeen = entityMovie.HasBeenSeen;
                        }
                    }
                }, ct.Token);
            }
            catch (Exception exception) when (exception is TaskCanceledException)
            {
                watch.Stop();
                Logger.Debug(
                    "ComputeMovieHistoryAsync cancelled.");
            }
            catch (Exception exception)
            {
                watch.Stop();
                Logger.Error(
                    $"ComputeMovieHistoryAsync: {exception.Message}");
            }

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Logger.Debug(
                $"ComputeMovieHistoryAsync in {elapsedMs} milliseconds.");
        }

        #endregion

        #region Method -> GetFavoritesMoviesAsync

        /// <summary>
        /// Get the favorites movies
        /// </summary>
        /// <param name="genre">The genre of the movies</param>
        /// <param name="ratingFilter">Used to filter by rating</param>
        /// <param name="ct">Userd to cancel task</param>
        /// <returns>Favorites movies</returns>
        public async Task<IEnumerable<MovieShort>> GetFavoritesMoviesAsync(MovieGenre genre, double ratingFilter,
            CancellationToken ct)
        {
            var watch = Stopwatch.StartNew();

            var movies = new List<MovieShort>();

            try
            {
                await Task.Run(async () =>
                {
                    using (var context = new ApplicationDbContext())
                    {
                        await context.MovieHistory.LoadAsync(ct);
                        var movieHistory = await context.MovieHistory.FirstOrDefaultAsync(ct);
                        if (genre != null)
                        {
                            movies.AddRange(movieHistory.MoviesShort.Where(
                                p =>
                                    p.IsFavorite && p.Genres.Any(g => g.Name == genre.EnglishName) &&
                                    p.Rating >= ratingFilter)
                                .Select(MovieShortFromEntityToModel));
                        }
                        else
                        {
                            movies.AddRange(movieHistory.MoviesShort.Where(
                                p => p.IsFavorite)
                                .Select(MovieShortFromEntityToModel));
                        }
                    }
                }, ct);
            }
            catch (Exception exception) when (exception is TaskCanceledException)
            {
                watch.Stop();
                Logger.Debug(
                    "GetFavoritesMoviesIdAsync cancelled.");
            }
            catch (Exception exception)
            {
                watch.Stop();
                Logger.Error(
                    $"GetFavoritesMoviesIdAsync: {exception.Message}");
            }

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Logger.Debug(
                $"GetFavoritesMoviesIdAsync in {elapsedMs} milliseconds.");
            return movies;
        }

        #endregion

        #region Method -> GetSeenMoviesAsync

        /// <summary>
        /// Get the seen movies
        /// </summary>
        /// <returns>Seen movies</returns>
        /// <param name="genre">The genre of the movies</param>
        /// <param name="ratingFilter">Used to filter by rating</param>
        /// <param name="ct">Used to cancel task</param>
        public async Task<IEnumerable<MovieShort>> GetSeenMoviesAsync(MovieGenre genre, double ratingFilter,
            CancellationTokenSource ct)
        {
            var watch = Stopwatch.StartNew();

            var movies = new List<MovieShort>();

            try
            {
                await Task.Run(async () =>
                {
                    using (var context = new ApplicationDbContext())
                    {
                        await context.MovieHistory.LoadAsync(ct.Token);
                        var movieHistory = await context.MovieHistory.FirstOrDefaultAsync(ct.Token);
                        if (genre != null)
                        {
                            movies.AddRange(movieHistory.MoviesShort.Where(
                                p =>
                                    p.HasBeenSeen && p.Genres.Any(g => g.Name == genre.EnglishName) &&
                                    p.Rating >= ratingFilter)
                                .Select(MovieShortFromEntityToModel));
                        }
                        else
                        {
                            movies.AddRange(movieHistory.MoviesShort.Where(
                                p => p.HasBeenSeen)
                                .Select(MovieShortFromEntityToModel));
                        }
                    }
                }, ct.Token);
            }
            catch (Exception exception) when (exception is TaskCanceledException)
            {
                watch.Stop();
                Logger.Debug(
                    "GetSeenMoviesIdAsync cancelled.");
            }
            catch (Exception exception)
            {
                watch.Stop();
                Logger.Error(
                    $"GetSeenMoviesIdAsync: {exception.Message}");
            }

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Logger.Debug(
                $"GetSeenMoviesIdAsync in {elapsedMs} milliseconds.");
            return movies;
        }

        #endregion

        #region Method -> SetFavoriteMovieAsync

        /// <summary>
        /// Set the movie as favorite
        /// </summary>
        /// <param name="movie">Favorite movie</param>
        /// <param name="ct">Used to cancel task</param>
        public static async Task SetFavoriteMovieAsync(MovieShort movie, CancellationTokenSource ct)
        {
            var watch = Stopwatch.StartNew();

            try
            {
                await Task.Run(async () =>
                {
                    using (var context = new ApplicationDbContext())
                    {
                        await context.MovieHistory.LoadAsync(ct.Token);
                        var movieHistory = await context.MovieHistory.FirstOrDefaultAsync(ct.Token);
                        if (movieHistory == null)
                        {
                            await CreateMovieHistoryAsync(ct);
                            movieHistory = await context.MovieHistory.FirstOrDefaultAsync(ct.Token);
                        }

                        if (movieHistory.MoviesShort == null)
                        {
                            movieHistory.MoviesShort = new List<Entity.Movie.MovieShort>
                            {
                                MovieShortFromModelToEntity(movie)
                            };

                            context.MovieHistory.AddOrUpdate(movieHistory);
                        }
                        else
                        {
                            var movieShort = movieHistory.MoviesShort.FirstOrDefault(p => p.MovieId == movie.Id);
                            if (movieShort == null)
                            {
                                movieHistory.MoviesShort.Add(MovieShortFromModelToEntity(movie));
                            }
                            else
                            {
                                movieShort.IsFavorite = movie.IsFavorite;
                            }
                        }

                        await context.SaveChangesAsync(ct.Token);
                    }
                }, ct.Token);
            }
            catch (Exception exception) when (exception is TaskCanceledException)
            {
                watch.Stop();
                Logger.Debug(
                    "SetFavoriteMovieAsync cancelled.");
            }
            catch (Exception exception)
            {
                watch.Stop();
                Logger.Error(
                    $"SetFavoriteMovieAsync: {exception.Message}");
            }

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Logger.Debug(
                $"SetFavoriteMovieAsync ({movie.ImdbCode}) in {elapsedMs} milliseconds.");
        }

        #endregion

        #region Method -> SetHasBeenSeenMovieAsync

        /// <summary>
        /// Set a movie as seen
        /// </summary>
        /// <param name="movie">Seen movie</param>
        /// <param name="ct">Used to cancel task</param>
        public async Task SetHasBeenSeenMovieAsync(MovieFull movie, CancellationTokenSource ct)
        {
            var watch = Stopwatch.StartNew();

            try
            {
                await Task.Run(async () =>
                {
                    using (var context = new ApplicationDbContext())
                    {
                        await context.MovieHistory.LoadAsync(ct.Token);
                        var movieHistory = await context.MovieHistory.FirstOrDefaultAsync(ct.Token);
                        if (movieHistory == null)
                        {
                            await CreateMovieHistoryAsync(ct);
                            movieHistory = await context.MovieHistory.FirstOrDefaultAsync(ct.Token);
                        }

                        if (movieHistory.MoviesFull == null)
                        {
                            movieHistory.MoviesFull = new List<Entity.Movie.MovieFull>
                            {
                                MovieFullFromModelToEntity(movie)
                            };

                            context.MovieHistory.AddOrUpdate(movieHistory);
                        }
                        else
                        {
                            var movieFull = movieHistory.MoviesFull.FirstOrDefault(p => p.MovieId == movie.Id);
                            if (movieFull == null)
                            {
                                movieHistory.MoviesFull.Add(MovieFullFromModelToEntity(movie));
                            }
                            else
                            {
                                movieFull.HasBeenSeen = movie.HasBeenSeen;
                            }
                        }

                        await context.SaveChangesAsync(ct.Token);
                    }
                }, ct.Token);
            }
            catch (Exception exception) when (exception is TaskCanceledException)
            {
                watch.Stop();
                Logger.Debug(
                    "SetHasBeenSeenMovieAsync cancelled.");
            }
            catch (Exception exception)
            {
                watch.Stop();
                Logger.Error(
                    $"SetHasBeenSeenMovieAsync: {exception.Message}");
            }

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Logger.Debug(
                $"SetHasBeenSeenMovieAsync ({movie.ImdbCode}) in {elapsedMs} milliseconds.");
        }

        #endregion

        #region Method -> CreateMovieHistoryAsync

        /// <summary>
        /// Scaffold UserData Table on database if empty
        /// </summary>
        /// <param name="ct">Used to cancel task</param>
        private static async Task CreateMovieHistoryAsync(CancellationTokenSource ct)
        {
            var watch = Stopwatch.StartNew();

            try
            {
                using (var context = new ApplicationDbContext())
                {
                    await context.MovieHistory.LoadAsync(ct.Token);
                    var userData = await context.MovieHistory.FirstOrDefaultAsync(ct.Token);
                    if (userData == null)
                    {
                        context.MovieHistory.AddOrUpdate(new MovieHistory
                        {
                            Created = DateTime.Now,
                            MoviesShort = new List<Entity.Movie.MovieShort>(),
                            MoviesFull = new List<Entity.Movie.MovieFull>()
                        });

                        await context.SaveChangesAsync(ct.Token);
                    }
                }
            }
            catch (Exception exception) when (exception is TaskCanceledException)
            {
                watch.Stop();
                Logger.Debug(
                    "CreateMovieHistoryAsync cancelled.");
            }
            catch (Exception exception)
            {
                watch.Stop();
                Logger.Error(
                    $"CreateMovieHistoryAsync: {exception.Message}");
            }

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Logger.Debug(
                $"CreateMovieHistoryAsync in {elapsedMs} milliseconds.");
        }

        #endregion

        #region Method -> MovieShortFromEntityToModel

        /// <summary>
        /// Convert a short movie entity to a short movie model
        /// </summary>
        /// <param name="movie">The movie to convert</param>
        /// <returns>Short movie model</returns>
        private static MovieShort MovieShortFromEntityToModel(Entity.Movie.MovieShort movie)
        {
            var torrents = movie.Torrents.Select(torrent => new TorrentDeserialized
            {
                DateUploaded = torrent.DateUploaded,
                Url = torrent.Url,
                Quality = torrent.Quality,
                DateUploadedMix = torrent.DateUploadedMix,
                Framerate = torrent.Framerate,
                Hash = torrent.Hash,
                Peers = torrent.Peers,
                Resolution = torrent.Resolution,
                Seeds = torrent.Seeds,
                Size = torrent.Size,
                SizeBytes = torrent.SizeBytes
            }).ToList();

            return new MovieShort
            {
                Language = movie.Language,
                ApiVersion = movie.ApiVersion,
                CoverImagePath = movie.CoverImagePath,
                DateUploaded = movie.DateUploaded,
                DateUploadedUnix = movie.DateUploadedUnix,
                ExecutionTime = movie.ExecutionTime,
                Genres = movie.Genres.Select(x => x.Name).ToList(),
                HasBeenSeen = movie.HasBeenSeen,
                Id = movie.MovieId,
                ImdbCode = movie.ImdbCode,
                IsFavorite = movie.IsFavorite,
                Runtime = movie.Runtime,
                RatingValue = movie.Rating,
                MpaRating = movie.MpaRating,
                Title = movie.Title,
                TitleLong = movie.TitleLong,
                Torrents = torrents,
                MediumCoverImage = movie.MediumCoverImage,
                Url = movie.Url,
                State = movie.State,
                ServerTimezone = movie.ServerTimezone,
                ServerTime = movie.ServerTime,
                SmallCoverImage = movie.SmallCoverImage,
                Year = movie.Year
            };
        }

        #endregion

        #region Method -> MovieShortFromModelToEntity

        /// <summary>
        /// Convert a short movie model to a short movie entity
        /// </summary>
        /// <param name="movie">The movie to convert</param>
        /// <returns>Short movie entity</returns>
        private static Entity.Movie.MovieShort MovieShortFromModelToEntity(MovieShort movie)
        {
            var torrents = movie.Torrents.Select(torrent => new Torrent
            {
                DateUploaded = torrent.DateUploaded,
                Url = torrent.Url,
                Quality = torrent.Quality,
                DateUploadedMix = torrent.DateUploadedMix,
                Framerate = torrent.Framerate,
                Hash = torrent.Hash,
                Peers = torrent.Peers,
                Resolution = torrent.Resolution,
                Seeds = torrent.Seeds,
                Size = torrent.Size,
                SizeBytes = torrent.SizeBytes
            }).ToList();

            var genres = movie.Genres.Select(genre => new Genre
            {
                Name = genre
            }).ToList();

            var movieShort = new Entity.Movie.MovieShort
            {
                MovieId = movie.Id,
                IsFavorite = movie.IsFavorite,
                HasBeenSeen = movie.HasBeenSeen,
                ServerTime = movie.ServerTime,
                ServerTimezone = movie.ServerTimezone,
                SmallCoverImage = movie.SmallCoverImage,
                State = movie.State,
                Year = movie.Year,
                Language = movie.Language,
                ImdbCode = movie.ImdbCode,
                Title = movie.Title,
                Id = movie.Id,
                DateUploaded = movie.DateUploaded,
                Runtime = movie.Runtime,
                Url = movie.Url,
                TitleLong = movie.TitleLong,
                Torrents = torrents,
                MediumCoverImage = movie.MediumCoverImage,
                Genres = genres,
                DateUploadedUnix = movie.DateUploadedUnix,
                CoverImagePath = movie.CoverImagePath,
                MpaRating = movie.MpaRating,
                Rating = movie.RatingValue,
                ExecutionTime = movie.ExecutionTime,
                ApiVersion = movie.ApiVersion
            };
            return movieShort;
        }

        #endregion

        #region Method -> MovieFullFromModelToEntity

        /// <summary>
        /// Convert a full movie model to a full movie entity
        /// </summary>
        /// <param name="movie">The movie to convert</param>
        /// <returns>Full movie entity</returns>
        private static Entity.Movie.MovieFull MovieFullFromModelToEntity(MovieFull movie)
        {
            var torrents = movie.Torrents.Select(torrent => new Torrent
            {
                DateUploaded = torrent.DateUploaded,
                Url = torrent.Url,
                Quality = torrent.Quality,
                DateUploadedMix = torrent.DateUploadedMix,
                Framerate = torrent.Framerate,
                Hash = torrent.Hash,
                Peers = torrent.Peers,
                Resolution = torrent.Resolution,
                Seeds = torrent.Seeds,
                Size = torrent.Size,
                SizeBytes = torrent.SizeBytes
            }).ToList();

            var genres = movie.Genres.Select(genre => new Genre
            {
                Name = genre
            }).ToList();

            var images = new Images
            {
                BackgroundImage = movie.Images.BackgroundImage,
                MediumCoverImage = movie.Images.MediumCoverImage,
                SmallCoverImage = movie.Images.SmallCoverImage,
                LargeCoverImage = movie.Images.LargeCoverImage,
                LargeScreenshotImage1 = movie.Images.LargeScreenshotImage1,
                LargeScreenshotImage2 = movie.Images.LargeScreenshotImage2,
                LargeScreenshotImage3 = movie.Images.MediumScreenshotImage3,
                MediumScreenshotImage3 = movie.Images.MediumScreenshotImage3,
                MediumScreenshotImage1 = movie.Images.MediumScreenshotImage1,
                MediumScreenshotImage2 = movie.Images.MediumScreenshotImage2
            };

            var actors = movie.Actors.Select(actor => new Actor
            {
                CharacterName = actor.CharacterName,
                MediumImage = actor.MediumImage,
                Name = actor.Name,
                SmallImage = actor.SmallImage,
                SmallImagePath = actor.SmallImagePath
            }).ToList();

            var directors = movie.Directors.Select(actor => new Director
            {
                MediumImage = actor.MediumImage,
                Name = actor.Name,
                SmallImage = actor.SmallImage,
                SmallImagePath = actor.SmallImagePath
            }).ToList();

            var movieFull = new Entity.Movie.MovieFull
            {
                MovieId = movie.Id,
                Year = movie.Year,
                Language = movie.Language,
                ImdbCode = movie.ImdbCode,
                Title = movie.Title,
                Id = movie.Id,
                DateUploaded = movie.DateUploaded,
                Runtime = movie.Runtime,
                Url = movie.Url,
                TitleLong = movie.TitleLong,
                Torrents = torrents,
                Genres = genres,
                DateUploadedUnix = movie.DateUploadedUnix,
                MpaRating = movie.MpaRating,
                Rating = movie.RatingValue,
                Images = images,
                DescriptionFull = movie.DescriptionFull,
                Actors = actors,
                Directors = directors,
                DescriptionIntro = movie.DescriptionIntro,
                DownloadCount = movie.DownloadCount,
                LikeCount = movie.LikeCount,
                RtAudienceRating = movie.RtAudienceRating,
                RtAudienceScore = movie.RtAudienceScore,
                RtCriticsRating = movie.RtCriticsRating,
                RtCrtiticsScore = movie.RtCrtiticsScore,
                YtTrailerCode = movie.YtTrailerCode,
                HasBeenSeen = movie.HasBeenSeen,
                IsFavorite = movie.IsFavorite
            };
            return movieFull;
        }

        #endregion

        #endregion
    }
}