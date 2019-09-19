using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using Console = Lucene.Net.Support.SystemConsole;
using Debug = Lucene.Net.Diagnostics.Debug; // LUCENENET NOTE: We cannot use System.Diagnostics.Debug because those calls will be optimized out of the release!

namespace Lucene.Net.Index
{
    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements.  See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License.  You may obtain a copy of the License at
     *
     *     http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    using Analyzer = Lucene.Net.Analysis.Analyzer;
    using BytesRef = Lucene.Net.Util.BytesRef;
    using Codec = Lucene.Net.Codecs.Codec;
    using Directory = Lucene.Net.Store.Directory;
    using InfoStream = Lucene.Net.Util.InfoStream;
    using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;
    using MockAnalyzer = Lucene.Net.Analysis.MockAnalyzer;
    using NullInfoStream = Lucene.Net.Util.NullInfoStream;
    using Query = Lucene.Net.Search.Query;
    using Similarity = Search.Similarities.Similarity;
    using TestUtil = Lucene.Net.Util.TestUtil;

    /// <summary>
    /// Silly class that randomizes the indexing experience.  EG
    /// it may swap in a different merge policy/scheduler; may
    /// commit periodically; may or may not forceMerge in the end,
    /// may flush by doc count instead of RAM, etc.
    /// </summary>
    public class RandomIndexWriter : IDisposable
    {
        public IndexWriter IndexWriter { get; set; } // LUCENENET: Renamed from w to IndexWriter to make it clear what this is.
        private readonly Random r;
        internal int docCount;
        internal int flushAt;
        private double flushAtFactor = 1.0;
        private bool getReaderCalled;
        private readonly Codec codec; // sugar

        public static IndexWriter MockIndexWriter(Directory dir, IndexWriterConfig conf, Random r)
        {
            // Randomly calls Thread.yield so we mixup thread scheduling
            Random random = new Random(r.Next());
            return MockIndexWriter(dir, conf, new TestPointAnonymousInnerClassHelper(random));
        }

        private class TestPointAnonymousInnerClassHelper : ITestPoint
        {
            private Random random;

            public TestPointAnonymousInnerClassHelper(Random random)
            {
                this.random = random;
            }

            public virtual void Apply(string message)
            {
                if (random.Next(4) == 2)
                {
                    System.Threading.Thread.Sleep(0);
                }
            }
        }

        public static IndexWriter MockIndexWriter(Directory dir, IndexWriterConfig conf, ITestPoint testPoint)
        {
            conf.SetInfoStream(new TestPointInfoStream(conf.InfoStream, testPoint));
            return new IndexWriter(dir, conf);
        }



#if FEATURE_STATIC_TESTDATA_INITIALIZATION
        /// <summary>
        /// Create a <see cref="RandomIndexWriter"/> with a random config: Uses <see cref="LuceneTestCase.TEST_VERSION_CURRENT"/> and <see cref="MockAnalyzer"/>.
        /// </summary>
        public RandomIndexWriter(Random r, Directory dir)
            : this(r, dir, LuceneTestCase.NewIndexWriterConfig(r, LuceneTestCase.TEST_VERSION_CURRENT, new MockAnalyzer(r)))
        {
        }

        /// <summary>
        /// Create a <see cref="RandomIndexWriter"/> with a random config: Uses <see cref="LuceneTestCase.TEST_VERSION_CURRENT"/>.
        /// </summary>
        public RandomIndexWriter(Random r, Directory dir, Analyzer a)
            : this(r, dir, LuceneTestCase.NewIndexWriterConfig(r, LuceneTestCase.TEST_VERSION_CURRENT, a))
        {
        }

        /// <summary>
        /// Creates a <see cref="RandomIndexWriter"/> with a random config.
        /// </summary>
        public RandomIndexWriter(Random r, Directory dir, LuceneVersion v, Analyzer a)
            : this(r, dir, LuceneTestCase.NewIndexWriterConfig(r, v, a))
        {
        }
#else
        /// <summary>
        /// Create a <see cref="RandomIndexWriter"/> with a random config: Uses <see cref="LuceneTestCase.TEST_VERSION_CURRENT"/> and <see cref="MockAnalyzer"/>.
        /// </summary>
        /// <param name="luceneTestCase">The current test instance.</param>
        /// <param name="r"></param>
        /// <param name="dir"></param>
        // LUCENENET specific
        // Similarity and TimeZone parameters allow a RandomIndexWriter to be
        // created without adding a dependency on 
        // <see cref="LuceneTestCase.ClassEnv.Similarity"/> and
        // <see cref="LuceneTestCase.ClassEnv.TimeZone"/>
        public RandomIndexWriter(LuceneTestCase luceneTestCase, Random r, Directory dir)
            : this(r, dir, luceneTestCase.NewIndexWriterConfig(r, LuceneTestCase.TEST_VERSION_CURRENT, new MockAnalyzer(r)))
        {
        }

        /// <summary>
        /// Create a <see cref="RandomIndexWriter"/> with a random config: Uses <see cref="LuceneTestCase.TEST_VERSION_CURRENT"/>.
        /// </summary>
        /// <param name="luceneTestCase">The current test instance.</param>
        /// <param name="r"></param>
        /// <param name="dir"></param>
        /// <param name="a"></param>
        // LUCENENET specific
        // Similarity and TimeZone parameters allow a RandomIndexWriter to be
        // created without adding a dependency on 
        // <see cref="LuceneTestCase.ClassEnv.Similarity"/> and
        // <see cref="LuceneTestCase.ClassEnv.TimeZone"/>
        public RandomIndexWriter(LuceneTestCase luceneTestCase, Random r, Directory dir, Analyzer a)
            : this(r, dir, luceneTestCase.NewIndexWriterConfig(r, LuceneTestCase.TEST_VERSION_CURRENT, a))
        {
        }

        /// <summary>
        /// Creates a <see cref="RandomIndexWriter"/> with a random config.
        /// </summary>
        /// <param name="luceneTestCase">The current test instance.</param>
        /// <param name="r"></param>
        /// <param name="dir"></param>
        /// <param name="v"></param>
        /// <param name="a"></param>

        // LUCENENET specific
        // Similarity and TimeZone parameters allow a RandomIndexWriter to be
        // created without adding a dependency on 
        // <see cref="LuceneTestCase.ClassEnv.Similarity"/> and
        // <see cref="LuceneTestCase.ClassEnv.TimeZone"/>
        public RandomIndexWriter(LuceneTestCase luceneTestCase, Random r, Directory dir, LuceneVersion v, Analyzer a)
            : this(r, dir, luceneTestCase.NewIndexWriterConfig(r, v, a))
        {
        }
#endif

        /// <summary>
        /// Creates a <see cref="RandomIndexWriter"/> with the provided config </summary>
        public RandomIndexWriter(Random r, Directory dir, IndexWriterConfig c)
        {
            // TODO: this should be solved in a different way; Random should not be shared (!).
            this.r = new Random(r.Next());
            IndexWriter = MockIndexWriter(dir, c, r);
            flushAt = TestUtil.NextInt32(r, 10, 1000);
            codec = IndexWriter.Config.Codec;
            if (LuceneTestCase.VERBOSE)
            {
                Console.WriteLine("RIW dir=" + dir + " config=" + IndexWriter.Config);
                Console.WriteLine("codec default=" + codec.Name);
            }

            // Make sure we sometimes test indices that don't get
            // any forced merges:
            doRandomForceMerge = !(c.MergePolicy is NoMergePolicy) && r.NextBoolean();
        }

        /// <summary>
        /// Adds a Document. </summary>
        /// <seealso cref="IndexWriter.AddDocument(IEnumerable{IIndexableField})"/>
        public virtual void AddDocument(IEnumerable<IIndexableField> doc)
        {
            AddDocument(doc, IndexWriter.Analyzer);
        }

        public virtual void AddDocument(IEnumerable<IIndexableField> doc, Analyzer a)
        {
            if (r.Next(5) == 3)
            {
                // TODO: maybe, we should simply buffer up added docs
                // (but we need to clone them), and only when
                // getReader, commit, etc. are called, we do an
                // addDocuments?  Would be better testing.
                IndexWriter.AddDocuments(new IterableAnonymousInnerClassHelper<IIndexableField>(this, doc), a);
            }
            else
            {
                IndexWriter.AddDocument(doc, a);
            }

            MaybeCommit();
        }

        private class IterableAnonymousInnerClassHelper<IndexableField> : IEnumerable<IEnumerable<IndexableField>>
        {
            private readonly RandomIndexWriter outerInstance;

            private IEnumerable<IndexableField> doc;

            public IterableAnonymousInnerClassHelper(RandomIndexWriter outerInstance, IEnumerable<IndexableField> doc)
            {
                this.outerInstance = outerInstance;
                this.doc = doc;
            }

            public IEnumerator<IEnumerable<IndexableField>> GetEnumerator()
            {
                return new IteratorAnonymousInnerClassHelper(this);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private class IteratorAnonymousInnerClassHelper : IEnumerator<IEnumerable<IndexableField>>
            {
                private readonly IterableAnonymousInnerClassHelper<IndexableField> outerInstance;

                public IteratorAnonymousInnerClassHelper(IterableAnonymousInnerClassHelper<IndexableField> outerInstance)
                {
                    this.outerInstance = outerInstance;
                }

                internal bool done;
                private IEnumerable<IndexableField> current;

                public bool MoveNext()
                {
                    if (done)
                    {
                        return false;
                    }

                    done = true;
                    current = outerInstance.doc;
                    return true;
                }

                public IEnumerable<IndexableField> Current
                {
                    get { return current; }
                }

                object System.Collections.IEnumerator.Current
                {
                    get { return Current; }
                }

                public void Reset()
                {
                    throw new NotImplementedException();
                }

                public void Dispose()
                {
                }
            }
        }

        private void MaybeCommit()
        {
            if (docCount++ == flushAt)
            {
                if (LuceneTestCase.VERBOSE)
                {
                    Console.WriteLine("RIW.add/updateDocument: now doing a commit at docCount=" + docCount);
                }
                IndexWriter.Commit();
                flushAt += TestUtil.NextInt32(r, (int)(flushAtFactor * 10), (int)(flushAtFactor * 1000));
                if (flushAtFactor < 2e6)
                {
                    // gradually but exponentially increase time b/w flushes
                    flushAtFactor *= 1.05;
                }
            }
        }

        public virtual void AddDocuments(IEnumerable<IEnumerable<IIndexableField>> docs)
        {
            IndexWriter.AddDocuments(docs);
            MaybeCommit();
        }

        public virtual void UpdateDocuments(Term delTerm, IEnumerable<IEnumerable<IIndexableField>> docs)
        {
            IndexWriter.UpdateDocuments(delTerm, docs);
            MaybeCommit();
        }

        /// <summary>
        /// Updates a document. </summary>
        /// <see cref="IndexWriter.UpdateDocument(Term, IEnumerable{IIndexableField})"/>
        public virtual void UpdateDocument(Term t, IEnumerable<IIndexableField> doc)
        {
            if (r.Next(5) == 3)
            {
                IndexWriter.UpdateDocuments(t, new IterableAnonymousInnerClassHelper2(this, doc));
            }
            else
            {
                IndexWriter.UpdateDocument(t, doc);
            }
            MaybeCommit();
        }

        private class IterableAnonymousInnerClassHelper2 : IEnumerable<IEnumerable<IIndexableField>>
        {
            private readonly RandomIndexWriter outerInstance;

            private IEnumerable<IIndexableField> doc;

            public IterableAnonymousInnerClassHelper2(RandomIndexWriter outerInstance, IEnumerable<IIndexableField> doc)
            {
                this.outerInstance = outerInstance;
                this.doc = doc;
            }

            public IEnumerator<IEnumerable<IIndexableField>> GetEnumerator()
            {
                return new IteratorAnonymousInnerClassHelper2(this);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private class IteratorAnonymousInnerClassHelper2 : IEnumerator<IEnumerable<IIndexableField>>
            {
                private readonly IterableAnonymousInnerClassHelper2 outerInstance;

                public IteratorAnonymousInnerClassHelper2(IterableAnonymousInnerClassHelper2 outerInstance)
                {
                    this.outerInstance = outerInstance;
                }

                internal bool done;
                private IEnumerable<IIndexableField> current;

                public bool MoveNext()
                {
                    if (done)
                    {
                        return false;
                    }

                    done = true;
                    current = outerInstance.doc;
                    return true;
                }

                public IEnumerable<IIndexableField> Current
                {
                    get { return current; }
                }

                object System.Collections.IEnumerator.Current
                {
                    get { return Current; }
                }

                public virtual void Reset()
                {
                    throw new NotImplementedException();
                }

                public void Dispose()
                {
                }
            }
        }

        public virtual void AddIndexes(params Directory[] dirs)
        {
            IndexWriter.AddIndexes(dirs);
        }

        public virtual void AddIndexes(params IndexReader[] readers)
        {
            IndexWriter.AddIndexes(readers);
        }

        public virtual void UpdateNumericDocValue(Term term, string field, long? value)
        {
            IndexWriter.UpdateNumericDocValue(term, field, value);
        }

        public virtual void UpdateBinaryDocValue(Term term, string field, BytesRef value)
        {
            IndexWriter.UpdateBinaryDocValue(term, field, value);
        }

        public virtual void DeleteDocuments(Term term)
        {
            IndexWriter.DeleteDocuments(term);
        }

        public virtual void DeleteDocuments(Query q)
        {
            IndexWriter.DeleteDocuments(q);
        }

        public virtual void Commit()
        {
            IndexWriter.Commit();
        }

        public virtual int NumDocs
        {
            get { return IndexWriter.NumDocs; }
        }

        public virtual int MaxDoc
        {
            get { return IndexWriter.MaxDoc; }
        }

        public virtual void DeleteAll()
        {
            IndexWriter.DeleteAll();
        }

        public virtual DirectoryReader GetReader()
        {
            return GetReader(true);
        }

        private bool doRandomForceMerge = true;
        private bool doRandomForceMergeAssert = true;

        public virtual void ForceMergeDeletes(bool doWait)
        {
            IndexWriter.ForceMergeDeletes(doWait);
        }

        public virtual void ForceMergeDeletes()
        {
            IndexWriter.ForceMergeDeletes();
        }

        public virtual bool DoRandomForceMerge
        {
            get // LUCENENET specific - added getter (to follow MSDN property guidelines)
            {
                return doRandomForceMerge;
            }
            set
            {
                doRandomForceMerge = value;
            }
        }

        public virtual bool DoRandomForceMergeAssert
        {
            get // LUCENENET specific - added getter (to follow MSDN property guidelines)
            {
                return doRandomForceMergeAssert;
            }
            set
            {
                doRandomForceMergeAssert = value;
            }
        }

        private void _DoRandomForceMerge() // LUCENENET specific - added leading underscore to keep this from colliding with the DoRandomForceMerge property
        {
            if (doRandomForceMerge)
            {
                int segCount = IndexWriter.SegmentCount;
                if (r.NextBoolean() || segCount == 0)
                {
                    // full forceMerge
                    if (LuceneTestCase.VERBOSE)
                    {
                        Console.WriteLine("RIW: doRandomForceMerge(1)");
                    }
                    IndexWriter.ForceMerge(1);
                }
                else
                {
                    // partial forceMerge
                    int limit = TestUtil.NextInt32(r, 1, segCount);
                    if (LuceneTestCase.VERBOSE)
                    {
                        Console.WriteLine("RIW: doRandomForceMerge(" + limit + ")");
                    }
                    IndexWriter.ForceMerge(limit);
                    Debug.Assert(!doRandomForceMergeAssert || IndexWriter.SegmentCount <= limit, "limit=" + limit + " actual=" + IndexWriter.SegmentCount);
                }
            }
        }

        public virtual DirectoryReader GetReader(bool applyDeletions)
        {
            getReaderCalled = true;
            if (r.Next(20) == 2)
            {
                _DoRandomForceMerge();
            }
            // If we are writing with PreFlexRW, force a full
            // IndexReader.open so terms are sorted in codepoint
            // order during searching:
            if (!applyDeletions || !codec.Name.Equals("Lucene3x", StringComparison.Ordinal) && r.NextBoolean())
            {
                if (LuceneTestCase.VERBOSE)
                {
                    Console.WriteLine("RIW.getReader: use NRT reader");
                }
                if (r.Next(5) == 1)
                {
                    IndexWriter.Commit();
                }
                return IndexWriter.GetReader(applyDeletions);
            }
            else
            {
                if (LuceneTestCase.VERBOSE)
                {
                    Console.WriteLine("RIW.getReader: open new reader");
                }
                IndexWriter.Commit();
                if (r.NextBoolean())
                {
                    return DirectoryReader.Open(IndexWriter.Directory, TestUtil.NextInt32(r, 1, 10));
                }
                else
                {
                    return IndexWriter.GetReader(applyDeletions);
                }
            }
        }

        // LUCENENET specific: Implemented dispose pattern

        /// <summary>
        /// Dispose this writer. </summary>
        /// <seealso cref="IndexWriter.Dispose()"/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose this writer. </summary>
        /// <seealso cref="IndexWriter.Dispose(bool)"/>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // if someone isn't using getReader() API, we want to be sure to
                // forceMerge since presumably they might open a reader on the dir.
                if (getReaderCalled == false && r.Next(8) == 2)
                {
                    _DoRandomForceMerge();
                }
                IndexWriter.Dispose();
            }
        }

        /// <summary>
        /// Forces a forceMerge.
        /// <para/>
        /// NOTE: this should be avoided in tests unless absolutely necessary,
        /// as it will result in less test coverage. </summary>
        /// <seealso cref="IndexWriter.ForceMerge(int)"/>
        public virtual void ForceMerge(int maxSegmentCount)
        {
            IndexWriter.ForceMerge(maxSegmentCount);
        }

        // LUCENENET specific - de-nested TestPointInfoStream

        // LUCENENET specific - de-nested ITestPoint
    }

    public sealed class TestPointInfoStream : InfoStream
    {
        private readonly InfoStream @delegate;
        private readonly ITestPoint testPoint;

        public TestPointInfoStream(InfoStream @delegate, ITestPoint testPoint)
        {
            this.@delegate = @delegate ?? new NullInfoStream();
            this.testPoint = testPoint;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                @delegate.Dispose();
            }
        }

        public override void Message(string component, string message)
        {
            if ("TP".Equals(component, StringComparison.Ordinal))
            {
                testPoint.Apply(message);
            }
            if (@delegate.IsEnabled(component))
            {
                @delegate.Message(component, message);
            }
        }

        public override bool IsEnabled(string component)
        {
            return "TP".Equals(component, StringComparison.Ordinal) || @delegate.IsEnabled(component);
        }
    }

    /// <summary>
    /// Simple interface that is executed for each <c>TP</c> <see cref="InfoStream"/> component
    /// message. See also <see cref="RandomIndexWriter.MockIndexWriter(Directory, IndexWriterConfig, ITestPoint)"/>.
    /// </summary>
    public interface ITestPoint
    {
        void Apply(string message);
    }
}